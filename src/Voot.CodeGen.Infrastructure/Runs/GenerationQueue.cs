using System.Threading.Channels;
using Voot.CodeGen.Application.Abstractions;

namespace Voot.CodeGen.Infrastructure.Runs;

/// <summary>
/// In-process queue between the request that submits a change and the worker that runs it.
/// Runs are also persisted with a Queued status, so a restart can recover anything that was
/// in flight rather than leaving it pending forever.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The type is a queue; the suffix is accurate.")]
public sealed class GenerationQueue : IGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
