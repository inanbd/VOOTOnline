using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Common;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Application.Services;

/// <summary>
/// Accepts a SQL change for a project and queues the run that will apply it and regenerate
/// the code. Returns as soon as the run is recorded; the work happens on the background worker.
/// </summary>
public sealed class ChangeSubmissionService(
    IGenerationRepository repository,
    ProjectAccessService access,
    IGenerationQueue queue,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>
    /// Records a SQL change and queues a run for it.
    /// </summary>
    /// <returns>The id of the queued run, for the status page to poll.</returns>
    public async Task<Guid> SubmitAsync(
        Guid projectId,
        string sqlText,
        string? title,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            throw new DomainException("Enter the SQL to apply.");
        }

        var project = await access.RequireAccessAsync(projectId, cancellationToken);

        if (!project.IsActive)
        {
            throw new DomainException("This project is inactive; reactivate it before submitting changes.");
        }

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

        return await QueueRunAsync(project.Id, change.Id, project.Settings.OutputStyle, cancellationToken);
    }

    /// <summary>Queues a run that regenerates from the current schema without applying any SQL.</summary>
    public async Task<Guid> RegenerateAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await access.RequireAccessAsync(projectId, cancellationToken);

        return await QueueRunAsync(project.Id, null, project.Settings.OutputStyle, cancellationToken);
    }

    private async Task<Guid> QueueRunAsync(
        Guid projectId, Guid? changeRequestId, OutputStyle style, CancellationToken cancellationToken)
    {
        var run = new GenerationRun
        {
            ProjectId = projectId,
            ChangeRequestId = changeRequestId,
            RequestedByUserId = currentUser.UserId!,
            RequestedByUserName = currentUser.UserName,
            OutputStyle = style,
            Status = RunStatus.Queued,
            Stage = RunStage.Queued,
            QueuedUtc = clock.UtcNow
        };

        await repository.AddRunAsync(run, cancellationToken);
        await queue.EnqueueAsync(run.Id, cancellationToken);

        return run.Id;
    }
}
