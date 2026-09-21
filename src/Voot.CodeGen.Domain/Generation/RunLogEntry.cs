namespace Voot.CodeGen.Domain.Generation;

/// <summary>A single timestamped line in a run's log, shown on the run detail page.</summary>
public sealed class RunLogEntry
{
    public long Id { get; set; }

    public required Guid RunId { get; set; }

    public DateTimeOffset LoggedUtc { get; set; } = DateTimeOffset.UtcNow;

    public RunLogLevel Level { get; set; } = RunLogLevel.Information;

    public RunStage Stage { get; set; }

    public required string Message { get; set; }

    /// <summary>Exception or SQL error detail; rendered in a collapsible block.</summary>
    public string? Detail { get; set; }

    public string? TableName { get; set; }
}
