using Workcast.Core.Events;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Broadcasts real-time <see cref="WorkcastEvent"/> instances to all connected SSE clients.
/// The implementation is a Singleton in-memory channel — no external message broker required.
/// </summary>
public interface IEventBroadcaster
{
    /// <summary>Publishes an event to every currently connected SSE client.</summary>
    Task PublishAsync(WorkcastEvent evt);

    /// <summary>
    /// Returns an async sequence of events for a single SSE client.
    /// The sequence ends when <paramref name="ct"/> is cancelled (i.e. when the client disconnects).
    /// </summary>
    IAsyncEnumerable<WorkcastEvent> SubscribeAsync(CancellationToken ct);
}
