using Microsoft.Extensions.Logging;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Domain.Generation;

namespace Voot.CodeGen.Infrastructure.Runs;

/// <summary>
/// Reconciles runs that were in flight when the process last stopped. Queued runs go back on
/// the queue; runs that were already executing are failed, because their SQL may have been
/// applied and re-running it blindly could double-apply a change.
/// </summary>
public sealed class RunRecoveryService(
    IGenerationRepository repository,
    IGenerationQueue queue,
    IClock clock,
    ILogger<RunRecoveryService> logger)
{
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = await repository.GetUnfinishedRunsAsync(cancellationToken);

        foreach (var run in unfinished)
        {
            if (run.Status == RunStatus.Queued)
            {
                logger.LogInformation("Re-queueing run {RunId} after restart.", run.Id);
                await queue.EnqueueAsync(run.Id, cancellationToken);
                continue;
            }

            logger.LogWarning("Failing run {RunId}, which was interrupted by a restart.", run.Id);

            run.Status = RunStatus.Failed;
            run.CompletedUtc = clock.UtcNow;
            run.ErrorMessage =
                "The application restarted while this run was executing. Any SQL it had already " +
                "applied is still applied; check the database before resubmitting.";
            run.ErrorCount++;

            await repository.UpdateRunAsync(run, cancellationToken);

            await repository.AddLogAsync(
                new RunLogEntry
                {
                    RunId = run.Id,
                    Level = RunLogLevel.Error,
                    Stage = run.Stage,
                    Message = run.ErrorMessage,
                    LoggedUtc = clock.UtcNow
                },
                cancellationToken);
        }
    }
}
