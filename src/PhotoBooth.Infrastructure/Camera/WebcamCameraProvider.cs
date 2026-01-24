using FlashCap;
using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Camera;

public class WebcamCameraProvider : ICameraProvider, IAsyncDisposable
{
    private CaptureDevice? _device;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private byte[]? _lastFrame;
    private readonly int _deviceIndex;
    private readonly ILogger<WebcamCameraProvider> _logger;

    /// <summary>
    /// Default capture latency for webcams (100ms).
    /// </summary>
    public static readonly TimeSpan DefaultCaptureLatency = TimeSpan.FromMilliseconds(100);

    public TimeSpan CaptureLatency { get; }

    public WebcamCameraProvider(
        ILogger<WebcamCameraProvider> logger,
        int deviceIndex = 0,
        TimeSpan? captureLatency = null)
    {
        _logger = logger;
        _deviceIndex = deviceIndex;
        CaptureLatency = captureLatency ?? DefaultCaptureLatency;

        _logger.LogInformation(
            "WebcamCameraProvider initialized with device index {DeviceIndex} and capture latency {CaptureLatencyMs}ms",
            _deviceIndex,
            CaptureLatency.TotalMilliseconds);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToList();
            var isAvailable = descriptors.Count > _deviceIndex;
            _logger.LogDebug("Camera availability check: {Available}, found {Count} devices", isAvailable, descriptors.Count);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking camera availability");
            return false;
        }
    }

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting camera capture from device {DeviceIndex}", _deviceIndex);
        await _captureLock.WaitAsync(cancellationToken);
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToList();

            if (descriptors.Count <= _deviceIndex)
            {
                _logger.LogError("Camera at index {DeviceIndex} not found, only {Count} devices available", _deviceIndex, descriptors.Count);
                throw new CameraNotAvailableException($"Camera at index {_deviceIndex} not found");
            }

            var descriptor = descriptors[_deviceIndex];
            _logger.LogDebug("Using camera: {Name}", descriptor.Name);

            var characteristics = descriptor.Characteristics
                .OrderByDescending(c => c.Width * c.Height)
                .FirstOrDefault();

            if (characteristics is null)
            {
                _logger.LogError("No suitable camera characteristics found for {Name}", descriptor.Name);
                throw new CameraNotAvailableException("No suitable camera characteristics found");
            }

            _logger.LogDebug("Using resolution {Width}x{Height}", characteristics.Width, characteristics.Height);

            _lastFrame = null;
            var frameReceived = new TaskCompletionSource<byte[]>();

            _device = await descriptor.OpenAsync(characteristics, async bufferScope =>
            {
                if (_lastFrame is null)
                {
                    var imageData = bufferScope.Buffer.ExtractImage();
                    _lastFrame = imageData;
                    frameReceived.TrySetResult(imageData);
                }
            });

            try
            {
                await _device.StartAsync(cancellationToken);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var completedTask = await Task.WhenAny(
                    frameReceived.Task,
                    Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask != frameReceived.Task)
                {
                    _logger.LogError("Timeout waiting for camera frame");
                    throw new CameraNotAvailableException("Timeout waiting for camera frame");
                }

                var result = await frameReceived.Task;
                _logger.LogInformation("Successfully captured frame with {Size} bytes", result.Length);
                return result;
            }
            finally
            {
                await _device.StopAsync(cancellationToken);
            }
        }
        catch (CameraNotAvailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture image");
            throw new CameraNotAvailableException("Failed to capture image", ex);
        }
        finally
        {
            if (_device is not null)
            {
                await _device.DisposeAsync();
                _device = null;
            }
            _captureLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_device is not null)
        {
            await _device.DisposeAsync();
            _device = null;
        }
        _captureLock.Dispose();
    }
}
