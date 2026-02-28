using Microsoft.Extensions.Logging;
using OpenCvSharp;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Imaging;

public class OpenCvImageResizer : IImageResizer
{
    private readonly IPhotoRepository _photoRepository;
    private readonly string _cacheDirectory;
    private readonly int _jpegQuality;
    private readonly ILogger<OpenCvImageResizer> _logger;

    public OpenCvImageResizer(
        IPhotoRepository photoRepository,
        string cacheDirectory,
        int jpegQuality,
        ILogger<OpenCvImageResizer> logger)
    {
        _photoRepository = photoRepository;
        _cacheDirectory = cacheDirectory;
        _jpegQuality = jpegQuality;
        _logger = logger;

        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<byte[]?> GetResizedImageAsync(Guid photoId, int width, CancellationToken ct)
    {
        var snappedWidth = IImageResizer.SnapToAllowedWidth(width);

        var cachePath = Path.Combine(_cacheDirectory, $"{photoId}_{snappedWidth}.jpg");

        if (File.Exists(cachePath))
        {
            _logger.LogDebug("Serving thumbnail from cache: {CachePath}", cachePath);
            return await File.ReadAllBytesAsync(cachePath, ct);
        }

        var originalData = await _photoRepository.GetImageDataAsync(photoId, ct);
        if (originalData is null)
        {
            return null;
        }

        var resized = ResizeImage(originalData, snappedWidth, photoId);

        await File.WriteAllBytesAsync(cachePath, resized, ct);
        _logger.LogDebug("Cached thumbnail: {CachePath} ({Bytes} bytes)", cachePath, resized.Length);

        return resized;
    }

    private byte[] ResizeImage(byte[] originalData, int targetWidth, Guid photoId)
    {
        using var src = Mat.FromImageData(originalData, ImreadModes.Color);

        if (src.Width <= targetWidth)
        {
            _logger.LogDebug("Photo {PhotoId} original width {Width} <= target {Target}, returning original", photoId, src.Width, targetWidth);
            return originalData;
        }

        var scale = (double)targetWidth / src.Width;
        var targetHeight = (int)(src.Height * scale);

        using var dst = new Mat();
        Cv2.Resize(src, dst, new Size(targetWidth, targetHeight), interpolation: InterpolationFlags.Area);

        var encodeParams = new ImageEncodingParam(ImwriteFlags.JpegQuality, _jpegQuality);
        Cv2.ImEncode(".jpg", dst, out var jpegData, encodeParams);

        _logger.LogDebug("Resized photo {PhotoId} from {OrigW}x{OrigH} to {NewW}x{NewH}, {Bytes} bytes",
            photoId, src.Width, src.Height, targetWidth, targetHeight, jpegData.Length);

        return jpegData;
    }
}
