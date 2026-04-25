using System.Runtime.InteropServices;
using OpenCvSharp;
using PhotoBooth.Domain.Entities;

namespace PhotoBooth.Server.Tests.LoadTesting;

internal sealed record SeededPhotos(string BasePath, string EventName, IReadOnlyList<Photo> Photos)
{
    public void Cleanup()
    {
        if (Directory.Exists(BasePath))
            Directory.Delete(BasePath, recursive: true);
    }
}

internal static class PhotoSeeder
{
    // Mirrors IImageResizer.AllowedWidths
    private static readonly int[] AllowedWidths = [200, 400, 800, 1200, 1920];

    public static SeededPhotos Seed(int photoCount)
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"photobooth-loadtest-{Guid.NewGuid():N}");
        const string eventName = "load-test";
        var eventDir = Path.Combine(basePath, eventName);
        var thumbnailDir = Path.Combine(basePath, ".thumbnails");
        Directory.CreateDirectory(eventDir);
        Directory.CreateDirectory(thumbnailDir);

        var photos = new List<Photo>(photoCount);
        var baseTime = DateTime.UtcNow.AddDays(-1);

        for (var i = 0; i < photoCount; i++)
        {
            var id = Guid.NewGuid();
            var code = (i + 1).ToString();
            var paddedCode = code.PadLeft(5, '0');
            var filePath = Path.Combine(eventDir, $"{paddedCode}-{id}.jpg");

            var jpeg = GenerateJpeg(seed: i);
            File.WriteAllBytes(filePath, jpeg);

            // Pre-warm the thumbnail cache matching OpenCvImageResizer's naming convention
            // so ThumbnailWarmupService (if still registered) finds nothing to do.
            PreGenerateThumbnails(jpeg, id, thumbnailDir);

            photos.Add(new Photo
            {
                Id = id,
                Code = code,
                FilePath = filePath,
                CapturedAt = baseTime.AddSeconds(i * 60)
            });
        }

        return new SeededPhotos(basePath, eventName, photos);
    }

    private static void PreGenerateThumbnails(byte[] originalJpeg, Guid photoId, string thumbnailDir)
    {
        using var src = Mat.FromImageData(originalJpeg, ImreadModes.Color);

        foreach (var targetWidth in AllowedWidths)
        {
            var cachePath = Path.Combine(thumbnailDir, $"{photoId}_{targetWidth}.jpg");

            if (src.Width <= targetWidth)
            {
                // OpenCvImageResizer returns original bytes without resizing and caches them
                File.WriteAllBytes(cachePath, originalJpeg);
                continue;
            }

            var scale = (double)targetWidth / src.Width;
            using var dst = new Mat();
            Cv2.Resize(src, dst, new Size(targetWidth, (int)(src.Height * scale)),
                interpolation: InterpolationFlags.Area);
            Cv2.ImEncode(".jpg", dst, out var thumbData,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 80));
            File.WriteAllBytes(cachePath, thumbData);
        }
    }

    // Generates a 1920×1080 JPEG with a colored gradient + hash-derived variation per seed.
    // The slight per-pixel variation prevents trivial JPEG compression so files have realistic
    // sizes (~80–200 KB) without requiring actual camera photos.
    private static byte[] GenerateJpeg(int seed)
    {
        const int Width = 1920;
        const int Height = 1080;

        var pixelData = new byte[Height * Width * 3];
        for (var i = 0; i < pixelData.Length; i += 3)
        {
            var pixel = i / 3;
            var row = pixel / Width;
            var col = pixel % Width;
            var h = (pixel * 1664525 + seed * 22695477) & 0xFFFFFF;
            pixelData[i]     = (byte)((col * 200 / Width     + (seed * 17 % 56) + (h & 0x1F))         & 0xFF);
            pixelData[i + 1] = (byte)((row * 200 / Height    + (seed * 31 % 56) + ((h >> 8) & 0x1F))  & 0xFF);
            pixelData[i + 2] = (byte)(((col + row) * 100 / (Width + Height) + (seed * 53 % 56) + ((h >> 16) & 0x1F)) & 0xFF);
        }

        using var mat = new Mat(Height, Width, MatType.CV_8UC3);
        Marshal.Copy(pixelData, 0, mat.Data, pixelData.Length);

        Cv2.ImEncode(".jpg", mat, out var jpegData, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
        return jpegData;
    }
}
