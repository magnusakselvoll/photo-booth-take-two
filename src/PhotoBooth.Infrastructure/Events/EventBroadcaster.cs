using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PhotoBooth.Application.Events;

namespace PhotoBooth.Infrastructure.Events;

public class EventBroadcaster : IEventBroadcaster
{
    private readonly List<Channel<PhotoBoothEvent>> _subscribers = [];
    private readonly Lock _lock = new();
    private readonly ILogger<EventBroadcaster> _logger;

    public EventBroadcaster(ILogger<EventBroadcaster> logger)
    {
        _logger = logger;
    }

    public async Task BroadcastAsync(PhotoBoothEvent evt, CancellationToken cancellationToken = default)
    {
        List<Channel<PhotoBoothEvent>> subscribers;
        lock (_lock)
        {
            subscribers = [.. _subscribers];
        }

        _logger.LogInformation("Broadcasting {EventType} to {SubscriberCount} subscribers",
            evt.EventType, subscribers.Count);

        var failedChannels = new List<Channel<PhotoBoothEvent>>();

        foreach (var channel in subscribers)
        {
            try
            {
                if (!channel.Writer.TryWrite(evt))
                {
                    // Channel is full or closed, mark for removal
                    failedChannels.Add(channel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write to subscriber channel");
                failedChannels.Add(channel);
            }
        }

        // Clean up failed channels
        if (failedChannels.Count > 0)
        {
            lock (_lock)
            {
                foreach (var channel in failedChannels)
                {
                    _subscribers.Remove(channel);
                }
            }
        }
    }

    public async IAsyncEnumerable<PhotoBoothEvent> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<PhotoBoothEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        _logger.LogInformation("New SSE subscriber connected. Total subscribers: {Count}", _subscribers.Count);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return evt;
            }
        }
        finally
        {
            lock (_lock)
            {
                _subscribers.Remove(channel);
            }
            channel.Writer.TryComplete();
            _logger.LogInformation("SSE subscriber disconnected. Total subscribers: {Count}", _subscribers.Count);
        }
    }
}
