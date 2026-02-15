namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Configuration options for OpenCV camera capture.
/// </summary>
public class OpenCvCameraOptions
{
    /// <summary>
    /// The camera device index. Default is 0 (first camera).
    /// </summary>
    public int DeviceIndex { get; set; } = 0;

    /// <summary>
    /// Capture latency in milliseconds. This is the delay between triggering capture
    /// and when the photo is taken, to allow for camera autofocus/exposure adjustment.
    /// </summary>
    public int CaptureLatencyMs { get; set; } = 100;

    /// <summary>
    /// Number of frames to skip when starting capture, to allow camera warm-up
    /// and auto-exposure adjustment.
    /// </summary>
    public int FramesToSkip { get; set; } = 5;

    /// <summary>
    /// Whether to flip the image vertically. Required for some cameras/platforms
    /// where the image data is returned upside down.
    /// </summary>
    public bool FlipVertical { get; set; } = false;

    /// <summary>
    /// JPEG encoding quality (1-100).
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>
    /// Preferred capture width. Set to 0 to use camera default.
    /// </summary>
    public int PreferredWidth { get; set; } = 1920;

    /// <summary>
    /// Preferred capture height. Set to 0 to use camera default.
    /// </summary>
    public int PreferredHeight { get; set; } = 1080;

    /// <summary>
    /// Time in milliseconds to warm up the camera after initialization.
    /// During warmup, frames are read and discarded to allow auto-exposure to settle.
    /// Set to 0 to disable warmup. Default is 500ms.
    /// </summary>
    public int InitializationWarmupMs { get; set; } = 500;

    /// <summary>
    /// Seconds to wait for the capture lock before reporting camera busy.
    /// </summary>
    public int CaptureLockTimeoutSeconds { get; set; } = 5;
}
