using PhotoBooth.Application.Events;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubEventBroadcaster : IEventBroadcaster
{
    public List<PhotoBoothEvent> BroadcastedEvents { get; } = [];

    public Task BroadcastAsync(PhotoBoothEvent evt, CancellationToken cancellationToken = default)
    {
        BroadcastedEvents.Add(evt);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<PhotoBoothEvent> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
