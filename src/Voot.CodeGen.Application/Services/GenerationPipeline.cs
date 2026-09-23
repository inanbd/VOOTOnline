using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Projects;
using Voot.CodeGen.Generation;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// The whole job for one run: apply the submitted SQL, read the resulting schema, generate
/// the files, zip them and record the artifact. Every stage writes to the run log, and any
/// failure is captured on the run so the UI can show it rather than swallowing it.
/// </summary>
public sealed class GenerationPipeline(
    IGenerationRepository repository,
    IProjectRepository projects,
    IConnectionStringProtector protector,
    ISqlScriptExecutor sqlExecutor,
    ISchemaReader schemaReader,
    ICodeGenerator generator,
    IArchiveBuilder archiveBuilder,
    IArtifactStorage storage,
    IClock clock)
{
    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await repository.GetRunAsync(runId, cancellationToken);

        if (run is null || run.IsTerminal)
        {
            return;
        }

        run.Status = RunStatus.Running;
        run.StartedUtc = clock.UtcNow;
        await repository.UpdateRunAsync(run, cancellationToken);

        try
        {
            var project = await projects.GetAsync(run.ProjectId, cancellationToken)
                ?? throw new InvalidOperationException("The project was deleted while the run was queued.");

            run.ProjectName = project.Name;
            var connectionString = protector.Unprotect(project.ProtectedConnectionString);

            if (!await ApplySqlAsync(run, project, connectionString, cancellationToken))
            {
                return;
            }

            var database = await ReadSchemaAsync(run, connectionString, cancellationToken);
            var fingerprint = SettingsFingerprint.Compute(project.Settings);
            var plan = await PlanScopeAsync(run, database, fingerprint, cancellationToken);
            var result = await GenerateAsync(run, database, project.Settings, plan.Selection, cancellationToken);
            await PackageAsync(run, project, result, cancellationToken);
            await SaveSnapshotAsync(run, database, fingerprint, result, cancellationToken);

            run.Status = RunStatus.Succeeded;
            run.Stage = RunStage.Completed;
            run.CompletedUtc = clock.UtcNow;
            await repository.UpdateRunAsync(run, cancellationToken);

            await LogAsync(run, RunLogLevel.Information, RunStage.Completed,
                $"Run completed in {run.Duration?.TotalSeconds:F1}s.", cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Status = RunStatus.Cancelled;
            run.CompletedUtc = clock.UtcNow;
            run.ErrorMessage = "The run was cancelled because the application shut down.";
            await repository.UpdateRunAsync(run, CancellationToken.None);
            await LogAsync(run, RunLogLevel.Warning, run.Stage, run.ErrorMessage, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            await FailAsync(run, run.Stage, ex.Message, ex.ToString(), CancellationToken.None);
        }
    }

    /// <summary>Runs the change request's SQL. Returns false when it failed and the run is over.</summary>
    private async Task<bool> ApplySqlAsync(
        GenerationRun run, Project project, string connectionString, CancellationToken cancellationToken)
    {
        if (run.ChangeRequestId is null)
        {
            await LogAsync(run, RunLogLevel.Information, RunStage.ExecutingSql,
                "No SQL change attached; regenerating from the current schema.", cancellationToken: cancellationToken);
            return true;
        }

        var change = await repository.GetChangeRequestAsync(run.ChangeRequestId.Value, cancellationToken);

        if (change is null)
        {
            await FailAsync(run, RunStage.ExecutingSql, "The submitted change could not be found.", null, cancellationToken);
            return false;
        }

        run.Stage = RunStage.ExecutingSql;
        await repository.UpdateRunAsync(run, cancellationToken);
        await LogAsync(run, RunLogLevel.Information, RunStage.ExecutingSql,
            $"Applying SQL change against {project.ConnectionStringSummary}.", cancellationToken: cancellationToken);

        SqlExecutionResult execution;

        try
        {
            execution = await sqlExecutor.ExecuteAsync(
                connectionString, change.SqlText, project.Settings.UseTransactionForSql, cancellationToken);
        }
        catch (Exception ex)
        {
            execution = SqlExecutionResult.Failure(ex.Message, 0);
        }

        change.BatchesExecuted = execution.BatchesExecuted;
        change.RowsAffected = execution.RowsAffected;

        if (!execution.Success)
        {
            change.Status = ChangeRequestStatus.Failed;
            change.ErrorMessage = execution.ErrorMessage;
            change.ErrorNumber = execution.ErrorNumber;
            change.ErrorLineNumber = execution.ErrorLineNumber;
            await repository.UpdateChangeRequestAsync(change, cancellationToken);

            var where = execution.FailedBatchIndex is { } index ? $" in batch {index + 1}" : string.Empty;
            var line = execution.ErrorLineNumber is { } l ? $", line {l}" : string.Empty;

            await FailAsync(
                run,
                RunStage.ExecutingSql,
                $"The SQL change failed{where}{line}: {execution.ErrorMessage}",
                execution.ErrorNumber is { } n ? $"SQL Server error {n}." : null,
                cancellationToken);

            return false;
        }

        change.Status = ChangeRequestStatus.Applied;
        change.AppliedUtc = clock.UtcNow;
        await repository.UpdateChangeRequestAsync(change, cancellationToken);

        await LogAsync(run, RunLogLevel.Information, RunStage.ExecutingSql,
            $"Applied {execution.BatchesExecuted} batch(es), {execution.RowsAffected} row(s) affected.",
            cancellationToken: cancellationToken);

        return true;
    }

    private async Task<Domain.Schema.DatabaseModel> ReadSchemaAsync(
        GenerationRun run, string connectionString, CancellationToken cancellationToken)
    {
        run.Stage = RunStage.ReadingSchema;
        await repository.UpdateRunAsync(run, cancellationToken);

        var database = await schemaReader.ReadAsync(connectionString, cancellationToken);

        await LogAsync(run, RunLogLevel.Information, RunStage.ReadingSchema,
            $"Read {database.Tables.Count} table(s) from {database.Name}.", cancellationToken: cancellationToken);

        return database;
    }

    /// <summary>
    /// Decides which tables to generate. A changed-tables request compares the schema with the
    /// snapshot of the last successful run, and falls back to every table when it cannot.
    /// </summary>
    private async Task<ScopePlan> PlanScopeAsync(
        GenerationRun run, Domain.Schema.DatabaseModel database, string fingerprint,
        CancellationToken cancellationToken)
    {
        var baseline = run.RequestedScope == GenerationScope.ChangedTables
            ? await repository.GetLatestSnapshotAsync(run.ProjectId, run.Id, cancellationToken)
            : null;

        var plan = GenerationScopePlanner.Plan(run.RequestedScope, baseline, database, fingerprint);

        run.Scope = plan.Scope;
        run.ScopeNote = plan.Note;
        await repository.UpdateRunAsync(run, cancellationToken);

        if (plan.Note is not null)
        {
            await LogAsync(run, RunLogLevel.Information, RunStage.GeneratingCode, plan.Note,
                cancellationToken: cancellationToken);
        }

        return plan;
    }

    /// <summary>
    /// Records the schema this run generated from, as the baseline for the next changed-tables
    /// run. The archive is already stored, so a failure here is logged rather than failing the run.
    /// </summary>
    private async Task SaveSnapshotAsync(
        GenerationRun run, Domain.Schema.DatabaseModel database, string fingerprint,
        GenerationResult result, CancellationToken cancellationToken)
    {
        // Tables that failed here are retried by the next changed-tables run even if unchanged.
        var failedTables = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.TableName is not null)
            .Select(d => d.TableName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        try
        {
            var snapshot = SchemaSnapshot.From(database, fingerprint, failedTables, clock.UtcNow);
            await repository.SaveSnapshotAsync(run.Id, run.ProjectId, snapshot, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await LogAsync(run, RunLogLevel.Warning, RunStage.PackagingArchive,
                "The schema snapshot could not be saved; the next changed-tables run will generate every table.",
                ex.ToString(), cancellationToken: cancellationToken);
        }
    }

    private async Task<GenerationResult> GenerateAsync(
        GenerationRun run, Domain.Schema.DatabaseModel database, GenerationSettings settings,
        TableSelection? selection, CancellationToken cancellationToken)
    {
        run.Stage = RunStage.GeneratingCode;
        await repository.UpdateRunAsync(run, cancellationToken);

        var result = generator.Generate(database, settings, selection);

        run.TableCount = result.TableCount;
        run.SkippedTableCount = result.SkippedTableCount;
        run.FileCount = result.Files.Count;
        run.WarningCount = result.WarningCount;
        run.ErrorCount = result.ErrorCount;

        foreach (var diagnostic in result.Diagnostics)
        {
            var level = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => RunLogLevel.Error,
                DiagnosticSeverity.Warning => RunLogLevel.Warning,
                _ => RunLogLevel.Information
            };

            await LogAsync(run, level, RunStage.GeneratingCode, diagnostic.Message,
                diagnostic.Detail, diagnostic.TableName, cancellationToken);
        }

        await LogAsync(run, RunLogLevel.Information, RunStage.GeneratingCode,
            $"Generated {result.Files.Count} file(s) from {result.TableCount} table(s); {result.SkippedTableCount} skipped.",
            cancellationToken: cancellationToken);

        return result;
    }

    private async Task PackageAsync(
        GenerationRun run, Project project, GenerationResult result, CancellationToken cancellationToken)
    {
        run.Stage = RunStage.PackagingArchive;
        await repository.UpdateRunAsync(run, cancellationToken);

        var fileName = BuildFileName(project.Name, clock.UtcNow);
        var entries = result.Files.Select(f => new ArchiveEntry(f.RelativePath, f.Content)).ToList();

        var (storagePath, sizeBytes, sha256) = await storage.SaveAsync(
            project.Id,
            run.Id,
            fileName,
            (stream, ct) => archiveBuilder.BuildAsync(entries, stream, ct),
            cancellationToken);

        var artifact = new GeneratedArtifact
        {
            RunId = run.Id,
            ProjectId = project.Id,
            FileName = fileName,
            StoragePath = storagePath,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            FileCount = result.Files.Count,
            CreatedUtc = clock.UtcNow
        };

        var fileEntries = result.Files
            .Select(f => new GeneratedFileEntry
            {
                ArtifactId = artifact.Id,
                RunId = run.Id,
                RelativePath = f.RelativePath,
                Emitter = f.Emitter,
                TableName = f.TableName,
                SizeBytes = System.Text.Encoding.UTF8.GetByteCount(f.Content)
            })
            .ToList();

        await repository.AddArtifactAsync(artifact, fileEntries, cancellationToken);
        run.ArtifactId = artifact.Id;

        await LogAsync(run, RunLogLevel.Information, RunStage.PackagingArchive,
            $"Packaged {fileName} ({sizeBytes:N0} bytes).", cancellationToken: cancellationToken);
    }

    /// <summary>A download name that sorts by time and carries no path separators.</summary>
    internal static string BuildFileName(string projectName, DateTimeOffset timestamp)
    {
        var safe = new string(projectName
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray())
            .Trim('-');

        if (safe.Length == 0)
        {
            safe = "project";
        }

        if (safe.Length > 60)
        {
            safe = safe[..60];
        }

        return $"{safe}-{timestamp:yyyyMMdd-HHmmss}.zip";
    }

    private async Task FailAsync(
        GenerationRun run, RunStage stage, string message, string? detail, CancellationToken cancellationToken)
    {
        run.Status = RunStatus.Failed;
        run.Stage = stage;
        run.CompletedUtc = clock.UtcNow;
        run.ErrorMessage = message;
        run.ErrorDetail = detail;
        run.ErrorCount++;

        await repository.UpdateRunAsync(run, cancellationToken);
        await LogAsync(run, RunLogLevel.Error, stage, message, detail, cancellationToken: cancellationToken);
    }

    private Task LogAsync(
        GenerationRun run,
        RunLogLevel level,
        RunStage stage,
        string message,
        string? detail = null,
        string? tableName = null,
        CancellationToken cancellationToken = default) =>
        repository.AddLogAsync(
            new RunLogEntry
            {
                RunId = run.Id,
                Level = level,
                Stage = stage,
                Message = message,
                Detail = detail,
                TableName = tableName,
                LoggedUtc = clock.UtcNow
            },
            cancellationToken);
}
