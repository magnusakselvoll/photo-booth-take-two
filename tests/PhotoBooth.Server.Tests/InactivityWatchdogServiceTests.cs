using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Application.Services;

namespace PhotoBooth.Server.Tests;

[TestClass]
public class InactivityWatchdogServiceTests
{
    private sealed class FakeActivityTracker : IActivityTracker
    {
        public TimeSpan TimeSinceLastActivity { get; set; }
        public void RecordActivity() { }
    }

    private sealed class FakeApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _startedSource = new();
        private readonly CancellationTokenSource _stoppingSource = new();
        private readonly CancellationTokenSource _stoppedSource = new();

        public bool StopApplicationCalled { get; private set; }
        public CancellationToken ApplicationStarted => _startedSource.Token;
        public CancellationToken ApplicationStopping => _stoppingSource.Token;
        public CancellationToken ApplicationStopped => _stoppedSource.Token;

        public void StopApplication() => StopApplicationCalled = true;
    }

    private static IConfiguration BuildConfig(int inactivityMinutes) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Watchdog:ServerInactivityMinutes"] = inactivityMinutes.ToString()
            })
            .Build();

    [TestMethod]
    public async Task WatchdogService_CallsStopApplication_WhenInactivityThresholdExceeded()
    {
        var fakeTracker = new FakeActivityTracker { TimeSinceLastActivity = TimeSpan.FromMinutes(31) };
        var fakeLifetime = new FakeApplicationLifetime();
        var config = BuildConfig(30);
        var logger = NullLogger<InactivityWatchdogService>.Instance;

        var service = new InactivityWatchdogService(
            fakeTracker, fakeLifetime, config, logger,
            checkInterval: TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Wait long enough for at least one check to complete
        await Task.Delay(300, CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.IsTrue(fakeLifetime.StopApplicationCalled,
            "StopApplication should have been called when inactivity exceeded the threshold");
    }

    [TestMethod]
    public async Task WatchdogService_DoesNotCallStopApplication_WhenActivityContinues()
    {
        var fakeTracker = new FakeActivityTracker { TimeSinceLastActivity = TimeSpan.FromMinutes(5) };
        var fakeLifetime = new FakeApplicationLifetime();
        var config = BuildConfig(30);
        var logger = NullLogger<InactivityWatchdogService>.Instance;

        var service = new InactivityWatchdogService(
            fakeTracker, fakeLifetime, config, logger,
            checkInterval: TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        // Run a few check cycles
        await Task.Delay(300, CancellationToken.None);

        await service.StopAsync(CancellationToken.None);

        Assert.IsFalse(fakeLifetime.StopApplicationCalled,
            "StopApplication should not have been called when activity is within threshold");
    }

    [TestMethod]
    public async Task WatchdogService_Disabled_WhenThresholdIsZero()
    {
        var fakeTracker = new FakeActivityTracker { TimeSinceLastActivity = TimeSpan.FromHours(24) };
        var fakeLifetime = new FakeApplicationLifetime();
        var config = BuildConfig(0);
        var logger = NullLogger<InactivityWatchdogService>.Instance;

        var service = new InactivityWatchdogService(
            fakeTracker, fakeLifetime, config, logger,
            checkInterval: TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await Task.Delay(300, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.IsFalse(fakeLifetime.StopApplicationCalled,
            "StopApplication should not be called when watchdog is disabled (threshold = 0)");
    }
}
