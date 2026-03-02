using PhotoBooth.Application.Services;

namespace PhotoBooth.Infrastructure.Monitoring;

public sealed class ActivityTracker : IActivityTracker
{
    private long _lastActivityTick = Environment.TickCount64;

    public void RecordActivity()
    {
        Interlocked.Exchange(ref _lastActivityTick, Environment.TickCount64);
    }

    public TimeSpan TimeSinceLastActivity =>
        TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastActivityTick));
}
