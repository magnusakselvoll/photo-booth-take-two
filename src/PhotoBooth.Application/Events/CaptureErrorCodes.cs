namespace PhotoBooth.Application.Events;

/// <summary>
/// Stable, machine-readable codes for <see cref="CaptureFailedEvent"/>.
/// The frontend maps these to localized strings; the event's English
/// <c>Error</c> text remains as a fallback for non-localizing clients.
/// </summary>
public static class CaptureErrorCodes
{
    public const string Timeout = "capture-timed-out";
    public const string StorageUnavailable = "storage-unavailable";
    public const string CaptureFailed = "capture-failed";
}
