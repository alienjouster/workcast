using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.Events;

/// <summary>
/// In-memory <see cref="IEventBroadcaster"/> implementation.
/// Maintains one bounded <see cref="Channel{T}"/> per connected SSE client.
/// Registered as Singleton so Hangfire jobs and API controllers share the same instance.
/// </summary>
public sealed class EventBroadcaster : IEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<WorkcastEvent>> _channels = new();

    /// <inheritdoc />
    public Task PublishAsync(WorkcastEvent evt)
    {
        foreach (var channel in _channels.Values)
            channel.Writer.TryWrite(evt); // Drop if client channel is full — non-blocking

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WorkcastEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var id      = Guid.NewGuid();
        var channel = Channel.CreateBounded<WorkcastEvent>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });

        _channels[id] = channel;

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                yield return evt;
        }
        finally
        {
            _channels.TryRemove(id, out _);
        }
    }
}
