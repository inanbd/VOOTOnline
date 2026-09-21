using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Read side of the audit trail: the change and run history for a project, each run's log,
/// and the list of files a run produced. Every method is access-checked against the project.
/// </summary>
public sealed class RunHistoryService(IGenerationRepository repository, ProjectAccessService access)
{
    public async Task<IReadOnlyList<GenerationRun>> GetRunsAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default)
    {
        await access.RequireAccessAsync(projectId, cancellationToken);
        return await repository.GetRunsAsync(projectId, take, cancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRequest>> GetChangesAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default)
    {
        await access.RequireAccessAsync(projectId, cancellationToken);
        return await repository.GetChangeRequestsAsync(projectId, take, cancellationToken);
    }

    /// <summary>A run with its log and file list, for the run detail page.</summary>
    public async Task<RunDetail> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await repository.GetRunAsync(runId, cancellationToken) ?? throw new NotFoundException("Run");
        await access.RequireAccessAsync(run.ProjectId, cancellationToken);

        var logs = await repository.GetLogsAsync(runId, cancellationToken);
        var change = run.ChangeRequestId is { } id
            ? await repository.GetChangeRequestAsync(id, cancellationToken)
            : null;

        var artifact = await repository.GetArtifactForRunAsync(runId, cancellationToken);
        var files = artifact is null
            ? []
            : await repository.GetArtifactFilesAsync(artifact.Id, cancellationToken);

        return new RunDetail(run, change, logs, artifact, files);
    }

    /// <summary>
    /// Lightweight status for the polling endpoint on the run page, so a browser watching a
    /// long run does not pull the whole log every second.
    /// </summary>
    public async Task<RunStatusView> GetStatusAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await repository.GetRunAsync(runId, cancellationToken) ?? throw new NotFoundException("Run");
        await access.RequireAccessAsync(run.ProjectId, cancellationToken);

        return new RunStatusView(
            run.Id,
            run.Status,
            run.Stage,
            run.IsTerminal,
            run.TableCount,
            run.FileCount,
            run.WarningCount,
            run.ErrorCount,
            run.ErrorMessage,
            run.ArtifactId);
    }
}

/// <summary>Everything the run detail page renders.</summary>
public sealed record RunDetail(
    GenerationRun Run,
    ChangeRequest? Change,
    IReadOnlyList<RunLogEntry> Logs,
    GeneratedArtifact? Artifact,
    IReadOnlyList<GeneratedFileEntry> Files);

/// <summary>The JSON shape the run page polls while a run is in flight.</summary>
public sealed record RunStatusView(
    Guid RunId,
    RunStatus Status,
    RunStage Stage,
    bool IsTerminal,
    int TableCount,
    int FileCount,
    int WarningCount,
    int ErrorCount,
    string? ErrorMessage,
    Guid? ArtifactId);
