using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Models;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Tracks which applied schema changes have reached development and production, and keeps the
/// history of every move.
/// </summary>
public sealed class DeploymentTrackingService(
    IGenerationRepository repository,
    ProjectAccessService access,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>Marks a single change as reaching an environment, or takes it back out.</summary>
    public async Task SetAsync(
        Guid projectId,
        Guid changeRequestId,
        DeploymentEnvironment environment,
        bool deployed,
        CancellationToken cancellationToken = default)
    {
        await access.RequireAccessAsync(projectId, cancellationToken);

        var change = await repository.GetChangeRequestAsync(changeRequestId, cancellationToken)
            ?? throw new NotFoundException("Change");

        // Guard against a change id from another project being passed in.
        if (change.ProjectId != projectId)
        {
            throw new NotFoundException("Change");
        }

        if (deployed && !change.IsAppliedSuccessfully)
        {
            throw new DomainException(
                "Only a change that applied successfully can be marked as deployed; this one never ran.");
        }

        // Already in the requested state, so there is nothing to record.
        if (change.IsDeployedTo(environment) == deployed)
        {
            return;
        }

        await repository.SetDeploymentAsync(
            changeRequestId, environment, deployed,
            currentUser.UserId!, currentUser.UserName, clock.UtcNow, cancellationToken);
    }

    /// <summary>
    /// Marks every change still outstanding for the environment. Returns how many were marked.
    /// </summary>
    public async Task<int> MarkAllPendingAsync(
        Guid projectId, DeploymentEnvironment environment, CancellationToken cancellationToken = default)
    {
        await access.RequireAccessAsync(projectId, cancellationToken);

        var pending = await repository.GetPendingChangesAsync(projectId, environment, cancellationToken);
        var now = clock.UtcNow;

        foreach (var change in pending)
        {
            // Marked one at a time so each move gets its own log entry, as a single bulk update
            // would lose the per-change history.
            await repository.SetDeploymentAsync(
                change.Id, environment, deployed: true,
                currentUser.UserId!, currentUser.UserName, now, cancellationToken);
        }

        return pending.Count;
    }

    /// <summary>The combined script for everything still outstanding in an environment.</summary>
    public async Task<PendingScript> GetPendingScriptAsync(
        Guid projectId, DeploymentEnvironment environment, CancellationToken cancellationToken = default)
    {
        var project = await access.RequireAccessAsync(projectId, cancellationToken);
        var pending = await repository.GetPendingChangesAsync(projectId, environment, cancellationToken);

        return new PendingScript(
            environment,
            pending.Count,
            PendingScriptBuilder.Build(project.Name, environment, pending, clock.UtcNow));
    }

    /// <summary>
    /// SQL changes and deployment moves merged into one newest-first sequence, for the timeline.
    /// </summary>
    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        Guid projectId, int take = 40, CancellationToken cancellationToken = default)
    {
        await access.RequireAccessAsync(projectId, cancellationToken);

        var changes = await repository.GetChangeRequestsAsync(projectId, take, cancellationToken);
        var moves = await repository.GetDeploymentLogAsync(projectId, take * 2, cancellationToken);

        var entries = new List<TimelineEntry>(changes.Count + moves.Count);

        entries.AddRange(changes.Select(c => new TimelineEntry(c.SubmittedUtc, c, null)));
        entries.AddRange(moves.Select(m => new TimelineEntry(m.MarkedUtc, null, m)));

        return [.. entries.OrderByDescending(e => e.OccurredUtc).Take(take)];
    }
}

/// <summary>A combined script plus how many changes went into it.</summary>
public sealed record PendingScript(DeploymentEnvironment Environment, int Count, string Sql)
{
    public bool IsEmpty => Count == 0;
}

/// <summary>
/// One moment on the project timeline: either a SQL change or a deployment move. Exactly one
/// of the two is set.
/// </summary>
public sealed record TimelineEntry(
    DateTimeOffset OccurredUtc,
    ChangeRequest? Change,
    ChangeDeployment? Deployment)
{
    public bool IsChange => Change is not null;
}
