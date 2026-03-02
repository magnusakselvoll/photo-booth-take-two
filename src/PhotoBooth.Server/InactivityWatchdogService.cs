using PhotoBooth.Application.Services;

namespace PhotoBooth.Server;

public sealed class InactivityWatchdogService : BackgroundService
{
    private readonly IActivityTracker _activityTracker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<InactivityWatchdogService> _logger;
    private readonly TimeSpan _inactivityThreshold;
    private readonly TimeSpan _checkInterval;

    public InactivityWatchdogService(
        IActivityTracker activityTracker,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration,
        ILogger<InactivityWatchdogService> logger,
        TimeSpan? checkInterval = null)
    {
        _activityTracker = activityTracker;
        _lifetime = lifetime;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(60);

        var minutes = configuration.GetValue<int?>("Watchdog:ServerInactivityMinutes") ?? 30;
        _inactivityThreshold = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_inactivityThreshold == TimeSpan.Zero)
        {
            _logger.LogInformation("Inactivity watchdog is disabled (threshold = 0)");
            return;
        }

        _logger.LogInformation(
            "Inactivity watchdog started (threshold: {Minutes} minutes, check interval: {Seconds}s)",
            _inactivityThreshold.TotalMinutes, _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var inactivity = _activityTracker.TimeSinceLastActivity;
            if (inactivity > _inactivityThreshold)
            {
                _logger.LogWarning(
                    "No API activity for {Minutes:F1} minutes (threshold: {Threshold} minutes). Shutting down for restart.",
                    inactivity.TotalMinutes, _inactivityThreshold.TotalMinutes);
                _lifetime.StopApplication();
                return;
            }
        }
    }
}
