using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Generation;
using Voot.CodeGen.Domain.Schema;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Applies a SQL change from the schema browser and reports what it did to the structure.
/// </summary>
/// <remarks>
/// This is the same audited path as a change submitted for generation — it writes the same
/// <see cref="ChangeRequest"/> row — but it does not queue a generation run. The schema page is
/// for inspecting the database; regenerating the code stays an explicit step.
/// </remarks>
public sealed class SchemaChangeService(
    ISchemaReader schemaReader,
    ISqlScriptExecutor sqlExecutor,
    IConnectionStringProtector protector,
    IGenerationRepository repository,
    ProjectAccessService access,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<SchemaChangeResult> ExecuteAsync(
        Guid projectId,
        string sqlText,
        string? title,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            throw new DomainException("Enter the SQL to run.");
        }

        var project = await access.RequireAccessAsync(projectId, cancellationToken);

        if (!project.IsActive)
        {
            throw new DomainException("This project is inactive; reactivate it before running SQL.");
        }

        var connectionString = protector.Unprotect(project.ProtectedConnectionString);

        // Recorded before execution, so a script that takes the server down is still on file.
        var change = new ChangeRequest
        {
            ProjectId = projectId,
            SubmittedByUserId = currentUser.UserId!,
            SubmittedByUserName = currentUser.UserName,
            SqlText = sqlText,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            SubmittedUtc = clock.UtcNow
        };

        await repository.AddChangeRequestAsync(change, cancellationToken);

        var before = await schemaReader.ReadAsync(connectionString, cancellationToken);

        SqlExecutionResult execution;

        try
        {
            execution = await sqlExecutor.ExecuteAsync(
                connectionString, sqlText, project.Settings.UseTransactionForSql, cancellationToken);
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

            // Nothing changed, so the snapshot taken beforehand is still current.
            return new SchemaChangeResult(change, execution, SchemaDiff.Empty, before);
        }

        // Re-read rather than infer: the diff then reflects what the server actually did,
        // including anything the script triggered indirectly.
        var after = await schemaReader.ReadAsync(connectionString, cancellationToken);
        var diff = SchemaComparer.Compare(before, after);

        change.Status = ChangeRequestStatus.Applied;
        change.AppliedUtc = clock.UtcNow;
        change.StructureSummary = diff.Summary();
        await repository.UpdateChangeRequestAsync(change, cancellationToken);

        return new SchemaChangeResult(change, execution, diff, after);
    }
}

/// <summary>The outcome of running SQL from the schema page.</summary>
/// <param name="Change">The audit row, carrying the script and its result.</param>
/// <param name="Execution">Batch and error detail from the server.</param>
/// <param name="Diff">What changed structurally; empty when the script failed.</param>
/// <param name="Schema">The database as it stands after the attempt, for re-rendering the page.</param>
public sealed record SchemaChangeResult(
    ChangeRequest Change,
    SqlExecutionResult Execution,
    SchemaDiff Diff,
    DatabaseModel Schema)
{
    public bool Succeeded => Execution.Success;
}
