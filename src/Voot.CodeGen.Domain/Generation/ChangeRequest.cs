namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// A SQL table change submitted against a project. The script is executed against the
/// project's own database; the text is retained permanently as the audit record.
/// </summary>
public sealed class ChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid ProjectId { get; set; }

    public required string SubmittedByUserId { get; set; }

    public string? SubmittedByUserName { get; set; }

    /// <summary>The raw script as typed by the user, including any <c>GO</c> batch separators.</summary>
    public required string SqlText { get; set; }

    /// <summary>Optional note explaining the change.</summary>
    public string? Title { get; set; }

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    public DateTimeOffset SubmittedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AppliedUtc { get; set; }

    /// <summary>Number of <c>GO</c>-delimited batches that executed successfully.</summary>
    public int BatchesExecuted { get; set; }

    public int RowsAffected { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>SQL Server error number, when the failure came from the server.</summary>
    public int? ErrorNumber { get; set; }

    public int? ErrorLineNumber { get; set; }
}
