namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Configuration options for webcam capture.
/// </summary>
public class WebcamOptions
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
    /// Number of frames to skip when starting capture, to allow camera warm-up.
    /// Some cameras return blank/blue frames initially.
    /// </summary>
    public int FramesToSkip { get; set; } = 5;

    /// <summary>
    /// Whether to flip the image vertically. Required for some cameras/platforms
    /// where the image data is returned upside down.
    /// </summary>
    public bool FlipVertical { get; set; } = true;

    /// <summary>
    /// Pixel byte order in 32-bit images. Different platforms use different orders.
    /// - "ARGB": Alpha at byte 0, then R, G, B (common on macOS)
    /// - "BGRA": Blue at byte 0, then G, R, A (common on Windows)
    /// </summary>
    public string PixelOrder { get; set; } = "ARGB";

    /// <summary>
    /// JPEG encoding quality (1-100).
    /// </summary>
    public int JpegQuality { get; set; } = 90;
}
