namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// One execution of the pipeline: apply SQL, read schema, generate files, build the archive.
/// Runs are queued and executed by a background worker so large databases do not block a request.
/// </summary>
public sealed class GenerationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid ProjectId { get; set; }

    public string? ProjectName { get; set; }

    /// <summary>Null when the run was started directly rather than by a SQL change.</summary>
    public Guid? ChangeRequestId { get; set; }

    public required string RequestedByUserId { get; set; }

    public string? RequestedByUserName { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Queued;

    public RunStage Stage { get; set; } = RunStage.Queued;

    public OutputStyle OutputStyle { get; set; }

    public DateTimeOffset QueuedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    public int TableCount { get; set; }

    public int SkippedTableCount { get; set; }

    public int FileCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    /// <summary>Populated when <see cref="Status"/> is <see cref="RunStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }

    public string? ErrorDetail { get; set; }

    /// <summary>Set once the archive has been written to storage.</summary>
    public Guid? ArtifactId { get; set; }

    public TimeSpan? Duration => StartedUtc is null || CompletedUtc is null
        ? null
        : CompletedUtc.Value - StartedUtc.Value;

    public bool IsTerminal => Status is RunStatus.Succeeded or RunStatus.Failed or RunStatus.Cancelled;
}
