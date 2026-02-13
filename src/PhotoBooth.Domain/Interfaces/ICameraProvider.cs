namespace PhotoBooth.Domain.Interfaces;

public interface ICameraProvider
{
    /// <summary>
    /// Captures a photo and returns the image data.
    /// </summary>
    Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the camera is available for capture.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares the camera for an upcoming capture. Called concurrently with the countdown
    /// so that slow setup (e.g., waking an Android device) overlaps with the countdown delay.
    /// Default implementation is a no-op for providers that don't need advance preparation.
    /// </summary>
    Task PrepareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// The expected latency from when capture is triggered to when the photo is actually taken.
    /// The UI should use this to adjust countdown timing so "0" aligns with the actual capture moment.
    /// </summary>
    TimeSpan CaptureLatency { get; }
}
