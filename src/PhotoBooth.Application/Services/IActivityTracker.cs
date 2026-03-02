namespace PhotoBooth.Application.Services;

public interface IActivityTracker
{
    void RecordActivity();
    TimeSpan TimeSinceLastActivity { get; }
}
