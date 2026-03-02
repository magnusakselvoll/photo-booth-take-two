using PhotoBooth.Infrastructure.Monitoring;

namespace PhotoBooth.Infrastructure.Tests.Monitoring;

[TestClass]
public class ActivityTrackerTests
{
    [TestMethod]
    public void InitialTimeSinceLastActivity_IsNearZero()
    {
        var tracker = new ActivityTracker();

        Assert.IsTrue(tracker.TimeSinceLastActivity < TimeSpan.FromSeconds(1),
            $"Expected near-zero initial inactivity, got {tracker.TimeSinceLastActivity}");
    }

    [TestMethod]
    public void RecordActivity_ResetsTimeSinceLastActivity()
    {
        var tracker = new ActivityTracker();

        // Wait a moment so there is measurable elapsed time
        Thread.Sleep(50);
        var before = tracker.TimeSinceLastActivity;

        tracker.RecordActivity();
        var after = tracker.TimeSinceLastActivity;

        Assert.IsTrue(after < before,
            $"Expected TimeSinceLastActivity to decrease after RecordActivity. Before: {before}, After: {after}");
    }

    [TestMethod]
    public async Task RecordActivity_ConcurrentCalls_DoNotThrow()
    {
        var tracker = new ActivityTracker();

        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() => tracker.RecordActivity()));
        await Task.WhenAll(tasks);

        // If we get here without exception, concurrency is handled correctly
        Assert.IsTrue(tracker.TimeSinceLastActivity < TimeSpan.FromSeconds(5));
    }
}
