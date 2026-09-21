namespace Voot.CodeGen.Application.Abstractions;

/// <summary>
/// Hands a queued run to the background worker. Submitting a change returns as soon as the
/// run is recorded, so a large database cannot time out the request.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The type is a queue; the suffix is accurate.")]
public interface IGenerationQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken);
}
