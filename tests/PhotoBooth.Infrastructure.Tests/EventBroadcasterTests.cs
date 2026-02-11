using Microsoft.Extensions.Logging;
using PhotoBooth.Application.Events;
using PhotoBooth.Infrastructure.Events;

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
public class EventBroadcasterTests
{
    private ILogger<EventBroadcaster> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<EventBroadcaster>();
    }

    [TestMethod]
    public async Task BroadcastAsync_WithNoSubscribers_DoesNotThrow()
    {
        var broadcaster = new EventBroadcaster(_logger);
        var evt = new CountdownStartedEvent(3000, "test");

        await broadcaster.BroadcastAsync(evt);
    }

    [TestMethod]
    public async Task SubscribeAsync_ReceivesBroadcastedEvent()
    {
        var broadcaster = new EventBroadcaster(_logger);
        var evt = new CountdownStartedEvent(3000, "test");

        using var cts = new CancellationTokenSource();
        PhotoBoothEvent? received = null;

        // Start subscription in background
        var subscribeTask = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
            {
                received = e;
                break; // Exit after first event
            }
        });

        // Give the subscriber time to connect
        await Task.Delay(50);

        // Broadcast
        await broadcaster.BroadcastAsync(evt);

        // Wait for subscription to process
        var completed = await Task.WhenAny(subscribeTask, Task.Delay(2000));
        Assert.AreEqual(subscribeTask, completed, "Subscription should have received the event");

        Assert.IsNotNull(received);
        Assert.IsInstanceOfType<CountdownStartedEvent>(received);
        Assert.AreEqual(3000, ((CountdownStartedEvent)received).DurationMs);
    }

    [TestMethod]
    public async Task BroadcastAsync_MultipleSubscribers_AllReceiveEvent()
    {
        var broadcaster = new EventBroadcaster(_logger);
        var evt = new PhotoCapturedEvent(Guid.NewGuid(), "1", "/api/photos/1/image");

        using var cts = new CancellationTokenSource();
        PhotoBoothEvent? received1 = null;
        PhotoBoothEvent? received2 = null;

        // Start two subscriptions
        var sub1 = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
            {
                received1 = e;
                break;
            }
        });

        var sub2 = Task.Run(async () =>
        {
            await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
            {
                received2 = e;
                break;
            }
        });

        await Task.Delay(50);

        await broadcaster.BroadcastAsync(evt);

        var bothDone = Task.WhenAll(sub1, sub2);
        var completed = await Task.WhenAny(bothDone, Task.Delay(2000));
        Assert.AreEqual(bothDone, completed, "Both subscribers should have received the event");

        Assert.IsNotNull(received1);
        Assert.IsNotNull(received2);
        Assert.IsInstanceOfType<PhotoCapturedEvent>(received1);
        Assert.IsInstanceOfType<PhotoCapturedEvent>(received2);
    }

    [TestMethod]
    public async Task SubscribeAsync_WhenCancelled_CleansUpSubscriber()
    {
        var broadcaster = new EventBroadcaster(_logger);

        using var cts = new CancellationTokenSource();
        var subscriptionStarted = new TaskCompletionSource();
        var subscriptionEnded = new TaskCompletionSource();

        var subscribeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var e in broadcaster.SubscribeAsync(cts.Token))
                {
                    subscriptionStarted.TrySetResult();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            subscriptionEnded.SetResult();
        });

        // Give the subscriber time to connect
        await Task.Delay(50);

        // Cancel and verify cleanup
        cts.Cancel();

        var completed = await Task.WhenAny(subscriptionEnded.Task, Task.Delay(2000));
        Assert.AreEqual(subscriptionEnded.Task, completed, "Subscription should have ended after cancellation");

        // After cleanup, broadcasting should not throw
        var evt = new CountdownStartedEvent(3000, "test");
        await broadcaster.BroadcastAsync(evt);
    }
}
