using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Voot.CodeGen.Application.Abstractions;
using Voot.CodeGen.Application.Services;

namespace Voot.CodeGen.Infrastructure.Runs;

/// <summary>
/// Drains the run queue one run at a time. Each run gets its own DI scope so a failure
/// cannot leak state into the next, and any exception is contained: the pipeline records the
/// failure on the run, and the worker keeps going.
/// </summary>
public sealed class GenerationWorker(
    IGenerationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<GenerationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Generation worker started.");

        await foreach (var runId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<GenerationPipeline>();

                await pipeline.ExecuteAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The pipeline records its own failures; reaching here means it could not.
                logger.LogError(ex, "Run {RunId} failed outside the pipeline's own error handling.", runId);
            }
        }

        logger.LogInformation("Generation worker stopped.");
    }
}
