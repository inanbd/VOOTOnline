using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Persistence for the change and run history. Everything here is append-mostly: the audit
/// trail is the point, so rows are updated in place only to advance a run's status.
/// </summary>
public interface IGenerationRepository
{
    // ---- change requests ----

    Task AddChangeRequestAsync(ChangeRequest request, CancellationToken cancellationToken = default);

    Task UpdateChangeRequestAsync(ChangeRequest request, CancellationToken cancellationToken = default);

    Task<ChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChangeRequest>> GetChangeRequestsAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a change moving into, or out of, an environment: updates the change's state and
    /// appends to the deployment log in one transaction.
    /// </summary>
    Task SetDeploymentAsync(
        Guid changeRequestId,
        DeploymentEnvironment environment,
        bool deployed,
        string userId,
        string? userName,
        DateTimeOffset markedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Applied changes that have not yet been marked as reaching the environment, oldest first.</summary>
    Task<IReadOnlyList<ChangeRequest>> GetPendingChangesAsync(
        Guid projectId,
        DeploymentEnvironment environment,
        CancellationToken cancellationToken = default);

    /// <summary>The history of deployment moves for a project, newest first.</summary>
    Task<IReadOnlyList<ChangeDeployment>> GetDeploymentLogAsync(
        Guid projectId, int take = 100, CancellationToken cancellationToken = default);

    // ---- runs ----

    Task AddRunAsync(GenerationRun run, CancellationToken cancellationToken = default);

    Task UpdateRunAsync(GenerationRun run, CancellationToken cancellationToken = default);

    Task<GenerationRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenerationRun>> GetRunsAsync(
        Guid projectId, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs left Queued or Running when the process stopped. Used at startup to fail them
    /// rather than leaving a spinner that never resolves.
    /// </summary>
    Task<IReadOnlyList<GenerationRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken = default);

    // ---- logs ----

    Task AddLogAsync(RunLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunLogEntry>> GetLogsAsync(Guid runId, CancellationToken cancellationToken = default);

    // ---- artifacts ----

    Task AddArtifactAsync(
        GeneratedArtifact artifact,
        IReadOnlyList<GeneratedFileEntry> files,
        CancellationToken cancellationToken = default);

    Task<GeneratedArtifact?> GetArtifactAsync(Guid id, CancellationToken cancellationToken = default);

    Task<GeneratedArtifact?> GetArtifactForRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedFileEntry>> GetArtifactFilesAsync(
        Guid artifactId, CancellationToken cancellationToken = default);

    Task MarkArtifactUnavailableAsync(Guid artifactId, CancellationToken cancellationToken = default);
}
