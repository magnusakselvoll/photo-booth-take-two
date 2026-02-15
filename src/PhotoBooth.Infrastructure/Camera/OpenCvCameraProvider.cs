using Microsoft.Extensions.Logging;
using OpenCvSharp;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Camera provider implementation using OpenCvSharp4.
/// Uses a persistent VideoCapture connection for stability.
/// </summary>
public class OpenCvCameraProvider : ICameraProvider, IDisposable
{
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly ILogger<OpenCvCameraProvider> _logger;
    private readonly OpenCvCameraOptions _options;

    private VideoCapture? _capture;
    private bool _isInitialized;
    private bool _disposed;

    public TimeSpan CaptureLatency { get; }

    public OpenCvCameraProvider(ILogger<OpenCvCameraProvider> logger, OpenCvCameraOptions options)
    {
        _logger = logger;
        _options = options;
        CaptureLatency = TimeSpan.FromMilliseconds(options.CaptureLatencyMs);

        _logger.LogInformation(
            "OpenCvCameraProvider initialized: device={DeviceIndex}, latency={CaptureLatencyMs}ms, framesToSkip={FramesToSkip}, flip={FlipVertical}, preferredRes={Width}x{Height}",
            _options.DeviceIndex,
            _options.CaptureLatencyMs,
            _options.FramesToSkip,
            _options.FlipVertical,
            _options.PreferredWidth,
            _options.PreferredHeight);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var testCapture = new VideoCapture(_options.DeviceIndex);
            var isAvailable = testCapture.IsOpened();
            _logger.LogDebug("Camera availability check: {Available} for device {DeviceIndex}", isAvailable, _options.DeviceIndex);
            return Task.FromResult(isAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking camera availability");
            return Task.FromResult(false);
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized && _capture is not null && _capture.IsOpened())
        {
            return;
        }

        _logger.LogInformation("Initializing OpenCV camera device...");

        CleanupCapture();

        _capture = new VideoCapture(_options.DeviceIndex);

        if (!_capture.IsOpened())
        {
            throw new CameraNotAvailableException($"Failed to open camera at index {_options.DeviceIndex}");
        }

        // Set preferred resolution if specified
        if (_options.PreferredWidth > 0 && _options.PreferredHeight > 0)
        {
            _capture.Set(VideoCaptureProperties.FrameWidth, _options.PreferredWidth);
            _capture.Set(VideoCaptureProperties.FrameHeight, _options.PreferredHeight);
        }

        // Read actual resolution
        var actualWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
        var actualHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);

        _logger.LogInformation("OpenCV camera opened: requested {ReqWidth}x{ReqHeight}, actual {ActWidth}x{ActHeight}",
            _options.PreferredWidth, _options.PreferredHeight, actualWidth, actualHeight);

        // Warmup: read frames to allow auto-exposure to settle
        if (_options.InitializationWarmupMs > 0)
        {
            var warmupStart = DateTime.UtcNow;
            var warmupDuration = TimeSpan.FromMilliseconds(_options.InitializationWarmupMs);
            using var warmupFrame = new Mat();
            var warmupFrameCount = 0;

            while (DateTime.UtcNow - warmupStart < warmupDuration)
            {
                _capture.Read(warmupFrame);
                warmupFrameCount++;
            }

            _logger.LogInformation("Camera warmup complete: {FrameCount} frames in {WarmupMs}ms",
                warmupFrameCount, _options.InitializationWarmupMs);
        }

        _isInitialized = true;
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _captureLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("PrepareAsync skipped — capture already in progress");
            return;
        }

        try
        {
            _logger.LogInformation("Preparing OpenCV camera for upcoming capture");
            EnsureInitialized();
            _logger.LogInformation("OpenCV camera preparation complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PrepareAsync failed — CaptureAsync will retry initialization");
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Starting OpenCV camera capture from device {DeviceIndex}", _options.DeviceIndex);

        if (!await _captureLock.WaitAsync(TimeSpan.FromSeconds(_options.CaptureLockTimeoutSeconds), cancellationToken))
        {
            throw new CameraNotAvailableException("Camera is busy");
        }

        try
        {
            EnsureInitialized();

            using var frame = new Mat();

            // Skip frames to allow auto-exposure adjustment
            for (var i = 0; i < _options.FramesToSkip; i++)
            {
                if (!_capture!.Read(frame))
                {
                    _logger.LogWarning("Failed to read frame {Index} during skip phase", i);
                }
                _logger.LogDebug("Skipped frame {Index}/{Total}", i + 1, _options.FramesToSkip);
            }

            // Capture the actual frame
            if (!_capture!.Read(frame))
            {
                _logger.LogError("Failed to read frame from camera");
                _isInitialized = false;
                throw new CameraNotAvailableException("Failed to read frame from camera");
            }

            if (frame.Empty())
            {
                _logger.LogError("Captured frame is empty");
                _isInitialized = false;
                throw new CameraNotAvailableException("Captured frame is empty");
            }

            _logger.LogDebug("Captured frame: {Width}x{Height}, type={Type}", frame.Width, frame.Height, frame.Type());

            // Flip if needed
            using var processedFrame = _options.FlipVertical ? frame.Flip(FlipMode.X) : frame;

            // Encode to JPEG
            var encodeParams = new ImageEncodingParam(ImwriteFlags.JpegQuality, _options.JpegQuality);
            Cv2.ImEncode(".jpg", processedFrame, out var jpegData, encodeParams);

            if (jpegData.Length == 0)
            {
                throw new CameraNotAvailableException("Failed to encode frame to JPEG");
            }

            _logger.LogInformation("Successfully captured frame: {Width}x{Height}, {Size} bytes JPEG",
                frame.Width, frame.Height, jpegData.Length);

            return jpegData;
        }
        catch (CameraNotAvailableException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new CameraNotAvailableException("Camera capture was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture image with OpenCV");
            _isInitialized = false;
            throw new CameraNotAvailableException("Failed to capture image", ex);
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _logger.LogDebug("Cleaning up OpenCV capture...");
            try
            {
                _capture.Release();
                _capture.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up OpenCV capture");
            }
            _capture = null;
        }
        _isInitialized = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupCapture();
        _captureLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
