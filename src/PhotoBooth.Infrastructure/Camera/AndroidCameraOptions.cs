namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Configuration options for Android phone camera capture via ADB.
/// </summary>
public class AndroidCameraOptions
{
    /// <summary>
    /// Path to the ADB executable. Default assumes ADB is on the system PATH.
    /// </summary>
    public string AdbPath { get; set; } = "adb";

    /// <summary>
    /// Folder on the Android device where the camera app saves photos.
    /// </summary>
    public string DeviceImageFolder { get; set; } = "/sdcard/DCIM/Camera";

    /// <summary>
    /// Optional PIN code to unlock the device screen.
    /// </summary>
    public string? PinCode { get; set; }

    /// <summary>
    /// Camera intent action to open the camera app (e.g., "STILL_IMAGE_CAMERA").
    /// </summary>
    public string CameraAction { get; set; } = "STILL_IMAGE_CAMERA";

    /// <summary>
    /// Interval in seconds between periodic focus keepalive commands.
    /// </summary>
    public int FocusKeepaliveIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Whether to delete photos from the device after downloading them.
    /// </summary>
    public bool DeleteAfterDownload { get; set; } = true;

    /// <summary>
    /// Regex pattern to match photo files on the device.
    /// </summary>
    public string FileSelectionRegex { get; set; } = @"^.*\.jpg$";

    /// <summary>
    /// Capture latency in milliseconds. This is the total pipeline latency from
    /// triggering capture to when the photo is actually taken, used to adjust
    /// countdown timing so "0" aligns with the actual capture moment.
    /// </summary>
    public int CaptureLatencyMs { get; set; } = 3000;

    /// <summary>
    /// Maximum time in milliseconds to wait for a new photo to appear on the device.
    /// </summary>
    public int CaptureTimeoutMs { get; set; } = 15000;

    /// <summary>
    /// Delay in milliseconds between file stability checks (two listings must match).
    /// </summary>
    public int FileStabilityDelayMs { get; set; } = 200;

    /// <summary>
    /// Polling interval in milliseconds when waiting for a new photo file.
    /// </summary>
    public int CapturePollingIntervalMs { get; set; } = 500;

    /// <summary>
    /// Timeout in milliseconds for individual ADB commands.
    /// </summary>
    public int AdbCommandTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Time in seconds after which the camera is considered stale and must be reopened.
    /// If the last camera action was longer ago than this, the device state is
    /// re-verified (wake, unlock, open camera) before the next capture.
    /// </summary>
    public int CameraOpenTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of capture retries after a failure. On retry, the provider
    /// performs full device recovery (wake, unlock, open camera) before re-attempting.
    /// </summary>
    public int MaxCaptureRetries { get; set; } = 1;
}
