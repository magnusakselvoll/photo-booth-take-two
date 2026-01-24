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
    /// The expected latency from when capture is triggered to when the photo is actually taken.
    /// The UI should use this to adjust countdown timing so "0" aligns with the actual capture moment.
    /// </summary>
    TimeSpan CaptureLatency { get; }
}
