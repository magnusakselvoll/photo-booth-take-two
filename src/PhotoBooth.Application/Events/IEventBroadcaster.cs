namespace PhotoBooth.Application.Events;

public interface IEventBroadcaster
{
    /// <summary>
    /// Broadcasts an event to all connected clients.
    /// </summary>
    Task BroadcastAsync(PhotoBoothEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to events. Returns an async enumerable of events.
    /// </summary>
    IAsyncEnumerable<PhotoBoothEvent> SubscribeAsync(CancellationToken cancellationToken);
}
