using FlashCap;
using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PhotoBooth.Infrastructure.Camera;

public class WebcamCameraProvider : ICameraProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly ILogger<WebcamCameraProvider> _logger;
    private readonly WebcamOptions _options;

    // Persistent device state
    private CaptureDevice? _device;
    private CaptureDeviceDescriptor? _descriptor;
    private VideoCharacteristics? _characteristics;
    private bool _isStreaming;
    private byte[]? _latestFrame;
    private TaskCompletionSource<byte[]>? _captureRequest;
    private int _frameCount;
    private int _framesToSkipForCapture;
    private bool _isInitialized;

    public TimeSpan CaptureLatency { get; }

    public WebcamCameraProvider(ILogger<WebcamCameraProvider> logger, WebcamOptions options)
    {
        _logger = logger;
        _options = options;
        CaptureLatency = TimeSpan.FromMilliseconds(options.CaptureLatencyMs);

        _logger.LogInformation(
            "WebcamCameraProvider initialized: device={DeviceIndex}, latency={CaptureLatencyMs}ms, framesToSkip={FramesToSkip}, flip={FlipVertical}, pixelOrder={PixelOrder}",
            _options.DeviceIndex,
            _options.CaptureLatencyMs,
            _options.FramesToSkip,
            _options.FlipVertical,
            _options.PixelOrder);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToList();
            var isAvailable = descriptors.Count > _options.DeviceIndex;
            _logger.LogDebug("Camera availability check: {Available}, found {Count} devices", isAvailable, descriptors.Count);
            return Task.FromResult(isAvailable);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking camera availability");
            return Task.FromResult(false);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized && _device is not null && _isStreaming)
        {
            return;
        }

        _logger.LogInformation("Initializing camera device...");

        // Clean up any existing device
        await CleanupDeviceAsync();

        var devices = new CaptureDevices();
        var descriptors = devices.EnumerateDescriptors().ToList();

        if (descriptors.Count <= _options.DeviceIndex)
        {
            throw new CameraNotAvailableException($"Camera at index {_options.DeviceIndex} not found");
        }

        _descriptor = descriptors[_options.DeviceIndex];
        _logger.LogInformation("Using camera: {Name}", _descriptor.Name);

        // Log available characteristics
        foreach (var c in _descriptor.Characteristics.Take(10))
        {
            _logger.LogDebug("Available: {Width}x{Height} at {Fps}fps, format {Format}",
                c.Width, c.Height, c.FramesPerSecond, c.PixelFormat);
        }

        // Select best characteristics
        _characteristics = _descriptor.Characteristics
            .Where(c => c.PixelFormat.ToString().Contains("JPEG", StringComparison.OrdinalIgnoreCase) ||
                       c.PixelFormat.ToString().Contains("MJPG", StringComparison.OrdinalIgnoreCase))
            .Where(c => c.Width >= 640 && c.Height >= 480)
            .OrderByDescending(c => c.Width * c.Height)
            .ThenByDescending(c => c.FramesPerSecond)
            .FirstOrDefault();

        _characteristics ??= _descriptor.Characteristics
            .Where(c => c.Width >= 640 && c.Height >= 480)
            .OrderByDescending(c => c.Width * c.Height)
            .ThenByDescending(c => c.FramesPerSecond)
            .FirstOrDefault();

        _characteristics ??= _descriptor.Characteristics
            .OrderByDescending(c => c.Width * c.Height)
            .FirstOrDefault();

        if (_characteristics is null)
        {
            throw new CameraNotAvailableException("No suitable camera characteristics found");
        }

        _logger.LogInformation("Using resolution {Width}x{Height} at {Fps}fps, format {Format}",
            _characteristics.Width, _characteristics.Height,
            _characteristics.FramesPerSecond, _characteristics.PixelFormat);

        _frameCount = 0;
        _framesToSkipForCapture = 0;
        _latestFrame = null;

        _device = await _descriptor.OpenAsync(_characteristics, OnFrameReceived);
        await _device.StartAsync(cancellationToken);
        _isStreaming = true;
        _isInitialized = true;

        _logger.LogInformation("Camera device initialized and streaming");

        // Brief pause to let camera start streaming
        await Task.Delay(100, cancellationToken);
    }

    private void OnFrameReceived(PixelBufferScope bufferScope)
    {
        _frameCount++;

        // If there's a pending capture request, handle it
        var request = _captureRequest;
        if (request is not null && !request.Task.IsCompleted)
        {
            // Skip frames for this capture to ensure camera has adjusted
            if (_framesToSkipForCapture > 0)
            {
                _framesToSkipForCapture--;
                _logger.LogDebug("Skipping frame for capture, {Remaining} remaining", _framesToSkipForCapture);
                return;
            }

            // Capture this frame
            try
            {
                var imageData = bufferScope.Buffer.ExtractImage();
                _latestFrame = imageData;
                request.TrySetResult(imageData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting frame");
                request.TrySetException(ex);
            }
        }
    }

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting camera capture from device {DeviceIndex}", _options.DeviceIndex);

        if (!await _captureLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            throw new CameraNotAvailableException("Camera is busy");
        }

        try
        {
            await EnsureInitializedAsync(cancellationToken);

            // Set up frame skipping for this capture and create request
            _framesToSkipForCapture = _options.FramesToSkip;
            _captureRequest = new TaskCompletionSource<byte[]>();

            _logger.LogDebug("Capture request created, will skip {FramesToSkip} frames first", _framesToSkipForCapture);

            // Wait for the frame
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var completedTask = await Task.WhenAny(
                _captureRequest.Task,
                Task.Delay(Timeout.Infinite, linkedCts.Token));

            if (completedTask != _captureRequest.Task)
            {
                _logger.LogError("Timeout waiting for camera frame");
                // Try to reinitialize on next capture
                _isInitialized = false;
                throw new CameraNotAvailableException("Timeout waiting for camera frame");
            }

            var rawData = await _captureRequest.Task;
            _logger.LogDebug("Received raw frame with {Size} bytes", rawData.Length);

            // Encode to JPEG
            var jpegData = EncodeToJpeg(rawData, _characteristics!.Width, _characteristics.Height, _characteristics.PixelFormat);
            _logger.LogInformation("Successfully captured frame with {Size} bytes", jpegData.Length);

            return jpegData;
        }
        catch (OperationCanceledException)
        {
            throw new CameraNotAvailableException("Camera capture was cancelled");
        }
        catch (CameraNotAvailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture image");
            // Try to reinitialize on next capture
            _isInitialized = false;
            throw new CameraNotAvailableException("Failed to capture image", ex);
        }
        finally
        {
            _captureRequest = null;
            _captureLock.Release();
        }
    }

    private async Task CleanupDeviceAsync()
    {
        if (_device is not null)
        {
            _logger.LogDebug("Cleaning up camera device...");
            try
            {
                if (_isStreaming)
                {
                    await _device.StopAsync();
                    _isStreaming = false;
                }
                await _device.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up camera device");
            }
            _device = null;
        }
        _isInitialized = false;
    }

    private byte[] EncodeToJpeg(byte[] imageData, int width, int height, FlashCap.PixelFormats pixelFormat)
    {
        _logger.LogInformation("Encoding image: {Width}x{Height}, format: {Format}, data size: {Size} bytes, header: {Header}",
            width, height, pixelFormat, imageData.Length,
            BitConverter.ToString(imageData, 0, Math.Min(20, imageData.Length)));

        // Check for JPEG magic bytes (FFD8FF)
        if (imageData.Length >= 3 && imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
        {
            _logger.LogInformation("Data is already JPEG, returning as-is");
            return imageData;
        }

        // Check for BMP file header (starts with "BM")
        if (imageData.Length >= 54 && imageData[0] == 0x42 && imageData[1] == 0x4D)
        {
            // Parse BMP header - FlashCap sometimes generates BMPs with incorrect dimensions in header
            var bmpWidth = BitConverter.ToInt32(imageData, 18);
            var originalBmpHeight = BitConverter.ToInt32(imageData, 22); // Keep original with sign
            var bitsPerPixel = BitConverter.ToInt16(imageData, 28);
            var pixelDataOffset = BitConverter.ToInt32(imageData, 10);

            var isBottomUp = originalBmpHeight > 0;
            var bmpHeight = Math.Abs(originalBmpHeight);
            var bytesPerPixel = bitsPerPixel / 8;
            var stride = ((bmpWidth * bytesPerPixel + 3) / 4) * 4;

            // Calculate actual height from available data (header may be wrong)
            var availablePixelData = imageData.Length - pixelDataOffset;
            var actualHeight = availablePixelData / stride;

            _logger.LogInformation("BMP header: {Width}x{OrigHeight} (abs={Height}), {Bpp}bpp, offset {Offset}, stride {Stride}, isBottomUp={IsBottomUp}",
                bmpWidth, originalBmpHeight, bmpHeight, bitsPerPixel, pixelDataOffset, stride, isBottomUp);

            // Check if the data actually fits the header dimensions
            var totalPixels = availablePixelData / bytesPerPixel;

            // FlashCap on macOS reports wrong dimensions - calculate actual dimensions from pixel count
            // MacBook Air camera outputs 1920 width, but height can vary (1080 or 1088 with padding)
            var commonWidths = new[] { 1920, 1280, 1440, 640 };
            var found = false;

            foreach (var w in commonWidths)
            {
                if (totalPixels % w == 0)
                {
                    var h = totalPixels / w;
                    // Sanity check - height should be reasonable
                    if (h >= 480 && h <= 1200)
                    {
                        _logger.LogInformation("Detected resolution {Width}x{Height} from {TotalPixels} pixels",
                            w, h, totalPixels);
                        bmpWidth = w;
                        bmpHeight = h;
                        stride = bmpWidth * bytesPerPixel;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                _logger.LogWarning("Could not detect resolution from {TotalPixels} pixels, using header values {Width}x{Height}",
                    totalPixels, bmpWidth, bmpHeight);
            }

            return EncodeBmpToJpeg(imageData, bmpWidth, bmpHeight, bytesPerPixel, stride, pixelDataOffset, isBottomUp);
        }

        // Check for DIB without file header (BITMAPINFOHEADER starts with size: 40, 108, or 124)
        // FlashCap's ExtractImage() often returns DIB format without the "BM" file header
        if (imageData.Length >= 40)
        {
            var headerSize = BitConverter.ToInt32(imageData, 0);
            if (headerSize == 40 || headerSize == 108 || headerSize == 124) // BITMAPINFOHEADER, BITMAPV4HEADER, BITMAPV5HEADER
            {
                var dibWidth = BitConverter.ToInt32(imageData, 4);
                var originalDibHeight = BitConverter.ToInt32(imageData, 8); // Keep original with sign
                var bitsPerPixel = BitConverter.ToInt16(imageData, 14);
                var compression = BitConverter.ToInt32(imageData, 16);

                // DIB height can be negative (top-down) or positive (bottom-up)
                var isBottomUp = originalDibHeight > 0;
                var dibHeight = Math.Abs(originalDibHeight);

                _logger.LogInformation("DIB header: size={HeaderSize}, {Width}x{OrigHeight} (abs={Height}), {Bpp}bpp, compression={Compression}, isBottomUp={IsBottomUp}",
                    headerSize, dibWidth, originalDibHeight, dibHeight, bitsPerPixel, compression, isBottomUp);

                var bytesPerPixel = bitsPerPixel / 8;
                var stride = ((dibWidth * bytesPerPixel + 3) / 4) * 4; // Rows are 4-byte aligned
                var pixelDataOffset = headerSize; // Pixel data starts right after header (no color table for 24/32-bit)

                // For images with color table, we'd need to skip it, but 24/32-bit BMPs typically don't have one
                if (bitsPerPixel <= 8)
                {
                    _logger.LogWarning("Paletted DIB format not supported, attempting fallback");
                }
                else
                {
                    // Calculate total pixels from available data
                    var availablePixelData = imageData.Length - pixelDataOffset;
                    var totalPixels = availablePixelData / bytesPerPixel;

                    // FlashCap on macOS reports wrong dimensions - calculate actual dimensions from pixel count
                    var commonWidths = new[] { 1920, 1280, 1440, 640 };
                    var found = false;

                    foreach (var w in commonWidths)
                    {
                        if (totalPixels % w == 0)
                        {
                            var h = totalPixels / w;
                            if (h >= 480 && h <= 1200)
                            {
                                _logger.LogInformation("DIB: Detected resolution {Width}x{Height} from {TotalPixels} pixels",
                                    w, h, totalPixels);
                                dibWidth = w;
                                dibHeight = h;
                                stride = dibWidth * bytesPerPixel;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        _logger.LogWarning("DIB: Could not detect resolution from {TotalPixels} pixels, using header {Width}x{Height}",
                            totalPixels, dibWidth, dibHeight);
                    }

                    return EncodeBmpToJpeg(imageData, dibWidth, dibHeight, bytesPerPixel, stride, pixelDataOffset, isBottomUp);
                }
            }
        }

        throw new InvalidOperationException($"Unsupported image format: {imageData.Length} bytes, header: {BitConverter.ToString(imageData, 0, Math.Min(16, imageData.Length))}");
    }

    private byte[] EncodeBmpToJpeg(byte[] imageData, int dibWidth, int dibHeight, int bytesPerPixel, int stride, int pixelDataOffset, bool isBottomUp)
    {
        _logger.LogInformation("EncodeBmpToJpeg: {Width}x{Height}, bpp={BytesPerPixel}, stride={Stride}, offset={Offset}, isBottomUp={IsBottomUp}, flipVertical={FlipVertical}",
            dibWidth, dibHeight, bytesPerPixel, stride, pixelDataOffset, isBottomUp, _options.FlipVertical);

        using var outputStream = new MemoryStream();

        if (bytesPerPixel == 3)
        {
            using var image = new Image<Rgb24>(dibWidth, dibHeight);
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < dibHeight; y++)
                {
                    // Calculate source row based on flip setting
                    int sourceY;
                    if (_options.FlipVertical)
                    {
                        sourceY = isBottomUp ? y : (dibHeight - 1 - y);
                    }
                    else
                    {
                        sourceY = isBottomUp ? (dibHeight - 1 - y) : y;
                    }
                    var rowStart = pixelDataOffset + sourceY * stride;

                    if (rowStart + dibWidth * 3 > imageData.Length)
                    {
                        _logger.LogWarning("Row {Y} exceeds data bounds, skipping", y);
                        return;
                    }

                    var sourceRow = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Bgr24>(
                        imageData.AsSpan(rowStart, dibWidth * 3));
                    var targetRow = accessor.GetRowSpan(y);

                    for (var x = 0; x < dibWidth; x++)
                    {
                        targetRow[x] = new Rgb24(sourceRow[x].R, sourceRow[x].G, sourceRow[x].B);
                    }
                }
            });
            image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = _options.JpegQuality });
        }
        else if (bytesPerPixel == 4)
        {
            // Determine pixel byte offsets based on configured pixel order
            var (rOffset, gOffset, bOffset) = _options.PixelOrder.ToUpperInvariant() switch
            {
                "ARGB" => (1, 2, 3), // Alpha at 0, then R, G, B
                "BGRA" => (2, 1, 0), // Blue at 0, then G, R, Alpha
                "RGBA" => (0, 1, 2), // Red at 0, then G, B, Alpha
                "ABGR" => (3, 2, 1), // Alpha at 0, then B, G, R
                _ => (1, 2, 3)       // Default to ARGB
            };

            using var image = new Image<Rgb24>(dibWidth, dibHeight);
            image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < dibHeight; y++)
                {
                    // Calculate source row based on flip setting
                    int sourceY;
                    if (_options.FlipVertical)
                    {
                        sourceY = isBottomUp ? y : (dibHeight - 1 - y);
                    }
                    else
                    {
                        sourceY = isBottomUp ? (dibHeight - 1 - y) : y;
                    }
                    var rowStart = pixelDataOffset + sourceY * stride;

                    if (rowStart + dibWidth * 4 > imageData.Length)
                    {
                        _logger.LogWarning("Row {Y} exceeds data bounds, skipping", y);
                        return;
                    }

                    var sourceBytes = imageData.AsSpan(rowStart, dibWidth * 4);
                    var targetRow = accessor.GetRowSpan(y);

                    for (var x = 0; x < dibWidth; x++)
                    {
                        var offset = x * 4;
                        targetRow[x] = new Rgb24(
                            sourceBytes[offset + rOffset],
                            sourceBytes[offset + gOffset],
                            sourceBytes[offset + bOffset]);
                    }
                }
            });
            image.SaveAsJpeg(outputStream, new JpegEncoder { Quality = _options.JpegQuality });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported DIB bits per pixel: {bytesPerPixel * 8}");
        }

        return outputStream.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupDeviceAsync();
        _captureLock.Dispose();
    }
}
