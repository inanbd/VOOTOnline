namespace Voot.CodeGen.Domain.Generation;

/// <summary>
/// One entry in the append-only record of a change being marked into, or taken back out of,
/// an environment. The current state lives on the change itself; this is the history of how it
/// got there, so "when did this reach production, and who said so?" always has an answer.
/// </summary>
public sealed class ChangeDeployment
{
    public long Id { get; set; }

    public required Guid ChangeRequestId { get; set; }

    public required Guid ProjectId { get; set; }

    public required DeploymentEnvironment Environment { get; set; }

    public required DeploymentAction Action { get; set; }

    public required string MarkedByUserId { get; set; }

    public string? MarkedByUserName { get; set; }

    public DateTimeOffset MarkedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Title of the change this refers to, denormalised so the timeline reads without a join.</summary>
    public string? ChangeTitle { get; set; }
}
