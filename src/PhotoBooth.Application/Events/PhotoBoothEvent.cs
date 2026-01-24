namespace PhotoBooth.Application.Events;

public abstract record PhotoBoothEvent(string EventType, DateTime Timestamp)
{
    public static DateTime Now => DateTime.UtcNow;
}

public record CountdownStartedEvent(
    int DurationMs,
    string TriggerSource,
    DateTime Timestamp) : PhotoBoothEvent("countdown-started", Timestamp)
{
    public CountdownStartedEvent(int durationMs, string triggerSource)
        : this(durationMs, triggerSource, Now) { }
}

public record PhotoCapturedEvent(
    Guid PhotoId,
    string Code,
    string ImageUrl,
    DateTime Timestamp) : PhotoBoothEvent("photo-captured", Timestamp)
{
    public PhotoCapturedEvent(Guid photoId, string code, string imageUrl)
        : this(photoId, code, imageUrl, Now) { }
}

public record CaptureFailedEvent(
    string Error,
    DateTime Timestamp) : PhotoBoothEvent("capture-failed", Timestamp)
{
    public CaptureFailedEvent(string error)
        : this(error, Now) { }
}
