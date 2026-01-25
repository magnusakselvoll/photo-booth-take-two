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
    private CaptureDevice? _device;
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private byte[]? _lastFrame;
    private readonly ILogger<WebcamCameraProvider> _logger;
    private readonly WebcamOptions _options;

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

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting camera capture from device {DeviceIndex}", _options.DeviceIndex);
        await _captureLock.WaitAsync(cancellationToken);
        try
        {
            var devices = new CaptureDevices();
            var descriptors = devices.EnumerateDescriptors().ToList();

            if (descriptors.Count <= _options.DeviceIndex)
            {
                _logger.LogError("Camera at index {DeviceIndex} not found, only {Count} devices available", _options.DeviceIndex, descriptors.Count);
                throw new CameraNotAvailableException($"Camera at index {_options.DeviceIndex} not found");
            }

            var descriptor = descriptors[_options.DeviceIndex];
            _logger.LogDebug("Using camera: {Name}", descriptor.Name);

            // Log all available characteristics
            foreach (var c in descriptor.Characteristics.Take(10))
            {
                _logger.LogInformation("Available: {Width}x{Height} at {Fps}fps, format {Format}",
                    c.Width, c.Height, c.FramesPerSecond, c.PixelFormat);
            }

            // Prefer MJPEG/JPEG format (gives us compressed frames directly)
            var characteristics = descriptor.Characteristics
                .Where(c => c.PixelFormat.ToString().Contains("JPEG", StringComparison.OrdinalIgnoreCase) ||
                           c.PixelFormat.ToString().Contains("MJPG", StringComparison.OrdinalIgnoreCase))
                .Where(c => c.Width >= 640 && c.Height >= 480)
                .OrderByDescending(c => c.Width * c.Height)
                .ThenByDescending(c => c.FramesPerSecond)
                .FirstOrDefault();

            // Fall back to any format if no JPEG available
            characteristics ??= descriptor.Characteristics
                .Where(c => c.Width >= 640 && c.Height >= 480)
                .OrderByDescending(c => c.Width * c.Height)
                .ThenByDescending(c => c.FramesPerSecond)
                .FirstOrDefault();

            characteristics ??= descriptor.Characteristics
                .OrderByDescending(c => c.Width * c.Height)
                .FirstOrDefault();

            if (characteristics is null)
            {
                _logger.LogError("No suitable camera characteristics found for {Name}", descriptor.Name);
                throw new CameraNotAvailableException("No suitable camera characteristics found");
            }

            _logger.LogInformation("Using resolution {Width}x{Height} at {Fps}fps, format {Format}",
                characteristics.Width, characteristics.Height,
                characteristics.FramesPerSecond, characteristics.PixelFormat);

            _lastFrame = null;
            var frameReceived = new TaskCompletionSource<byte[]>();
            var frameCount = 0;
            var framesToSkip = _options.FramesToSkip;

            var pixelFormat = characteristics.PixelFormat;
            _device = await descriptor.OpenAsync(characteristics, async bufferScope =>
            {
                frameCount++;
                if (frameCount <= framesToSkip)
                {
                    _logger.LogDebug("Skipping warm-up frame {FrameNumber}/{FramesToSkip}", frameCount, framesToSkip);
                    return;
                }

                if (_lastFrame is null)
                {
                    try
                    {
                        _logger.LogDebug("Capturing frame {FrameNumber} (after {Skipped} warm-up frames)", frameCount, framesToSkip);
                        var imageData = bufferScope.Buffer.ExtractImage();
                        var jpegData = EncodeToJpeg(imageData, characteristics.Width, characteristics.Height, pixelFormat);
                        _lastFrame = jpegData;
                        frameReceived.TrySetResult(jpegData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to encode frame to JPEG");
                        frameReceived.TrySetException(ex);
                    }
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

    private byte[] EncodeToJpeg(byte[] imageData, int width, int height, FlashCap.PixelFormats pixelFormat)
    {
        _logger.LogDebug("Encoding image: {Width}x{Height}, format: {Format}, data size: {Size} bytes",
            width, height, pixelFormat, imageData.Length);

        // Check for JPEG magic bytes (FFD8FF)
        if (imageData.Length >= 3 && imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
        {
            _logger.LogDebug("Data is already JPEG, returning as-is");
            return imageData;
        }

        // Check for BMP file header (starts with "BM")
        if (imageData.Length >= 54 && imageData[0] == 0x42 && imageData[1] == 0x4D)
        {
            // Parse BMP header - FlashCap sometimes generates BMPs with incorrect dimensions in header
            var bmpWidth = BitConverter.ToInt32(imageData, 18);
            var bmpHeight = BitConverter.ToInt32(imageData, 22);
            var bitsPerPixel = BitConverter.ToInt16(imageData, 28);
            var pixelDataOffset = BitConverter.ToInt32(imageData, 10);

            var isBottomUp = bmpHeight > 0;
            bmpHeight = Math.Abs(bmpHeight);
            var bytesPerPixel = bitsPerPixel / 8;
            var stride = ((bmpWidth * bytesPerPixel + 3) / 4) * 4;

            // Calculate actual height from available data (header may be wrong)
            var availablePixelData = imageData.Length - pixelDataOffset;
            var actualHeight = availablePixelData / stride;

            _logger.LogDebug("BMP file header: {Width}x{Height}, {Bpp}bpp, offset {Offset}, stride {Stride}",
                bmpWidth, bmpHeight, bitsPerPixel, pixelDataOffset, stride);

            // Check if the data actually fits the header dimensions
            var totalPixels = availablePixelData / bytesPerPixel;
            var headerPixels = bmpWidth * actualHeight;

            if (totalPixels != headerPixels || actualHeight != bmpHeight)
            {
                // Header dimensions don't match data - try to find correct dimensions
                // Check common video resolutions first
                var commonResolutions = new[] { (1920, 1080), (1280, 720), (1440, 1080), (1440, 960), (640, 480) };
                var found = false;

                foreach (var (w, h) in commonResolutions)
                {
                    if (w * h == totalPixels)
                    {
                        _logger.LogWarning("BMP header dimensions wrong! Data matches {Width}x{Height} ({TotalPixels} pixels), header claims {HeaderWidth}x{HeaderHeight}",
                            w, h, totalPixels, bmpWidth, bmpHeight);
                        bmpWidth = w;
                        bmpHeight = h;
                        stride = bmpWidth * bytesPerPixel;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Check if it's a square image
                    var sqrtPixels = (int)Math.Sqrt(totalPixels);
                    if (sqrtPixels * sqrtPixels == totalPixels)
                    {
                        _logger.LogWarning("BMP header dimensions wrong! Data suggests {Sqrt}x{Sqrt} image ({TotalPixels} pixels), header claims {Width}x{Height}",
                            sqrtPixels, sqrtPixels, totalPixels, bmpWidth, bmpHeight);
                        bmpWidth = sqrtPixels;
                        bmpHeight = sqrtPixels;
                        stride = bmpWidth * bytesPerPixel;
                    }
                    else
                    {
                        _logger.LogWarning("BMP header height mismatch! Using calculated height {ActualHeight} instead of {HeaderHeight}",
                            actualHeight, bmpHeight);
                        bmpHeight = actualHeight;
                    }
                }
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
                var dibHeight = BitConverter.ToInt32(imageData, 8);
                var bitsPerPixel = BitConverter.ToInt16(imageData, 14);
                var compression = BitConverter.ToInt32(imageData, 16);

                _logger.LogInformation("DIB header detected: size={HeaderSize}, {Width}x{Height}, {Bpp}bpp, compression={Compression}",
                    headerSize, dibWidth, dibHeight, bitsPerPixel, compression);

                // DIB height can be negative (top-down) or positive (bottom-up)
                var isBottomUp = dibHeight > 0;
                dibHeight = Math.Abs(dibHeight);

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
                    // Calculate actual height from available data (header may be wrong)
                    var availablePixelData = imageData.Length - pixelDataOffset;
                    var actualHeight = availablePixelData / stride;

                    _logger.LogInformation("DIB stride: {Stride}, pixel offset: {Offset}, bottomUp: {BottomUp}, actual height: {ActualHeight}",
                        stride, pixelDataOffset, isBottomUp, actualHeight);

                    if (actualHeight != dibHeight)
                    {
                        _logger.LogWarning("DIB header height mismatch! Using calculated height {ActualHeight} instead of {HeaderHeight}",
                            actualHeight, dibHeight);
                        dibHeight = actualHeight;
                    }

                    return EncodeBmpToJpeg(imageData, dibWidth, dibHeight, bytesPerPixel, stride, pixelDataOffset, isBottomUp);
                }
            }
        }

        throw new InvalidOperationException($"Unsupported image format: {imageData.Length} bytes, header: {BitConverter.ToString(imageData, 0, Math.Min(16, imageData.Length))}");
    }

    private byte[] EncodeBmpToJpeg(byte[] imageData, int dibWidth, int dibHeight, int bytesPerPixel, int stride, int pixelDataOffset, bool isBottomUp)
    {
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
        if (_device is not null)
        {
            await _device.DisposeAsync();
            _device = null;
        }
        _captureLock.Dispose();
    }
}
