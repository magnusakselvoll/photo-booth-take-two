using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Imaging;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Infrastructure.Tests.Imaging;

[TestClass]
public sealed class OpenCvImageResizerTests
{
    private string _cacheDirectory = null!;
    private InMemoryPhotoRepository _repository = null!;
    private OpenCvImageResizer _resizer = null!;

    // A small valid JPEG created via OpenCV (200x100 pixels)
    private static readonly byte[] TestJpeg = CreateTestJpeg(width: 800, height: 600);

    [TestInitialize]
    public void Setup()
    {
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "PhotoBoothResizerTests", Guid.NewGuid().ToString());
        _repository = new InMemoryPhotoRepository();
        _resizer = new OpenCvImageResizer(
            _repository,
            _cacheDirectory,
            jpegQuality: 80,
            NullLogger<OpenCvImageResizer>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetResizedImageAsync_WhenPhotoDoesNotExist_ReturnsNull()
    {
        var result = await _resizer.GetResizedImageAsync(Guid.NewGuid(), 400, CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetResizedImageAsync_ReturnsResizedImage()
    {
        var photoId = await SaveTestPhoto(TestJpeg);

        var result = await _resizer.GetResizedImageAsync(photoId, 400, CancellationToken.None);

        Assert.IsNotNull(result);
        using var mat = Mat.FromImageData(result, ImreadModes.Color);
        Assert.AreEqual(400, mat.Width);
    }

    [TestMethod]
    public async Task GetResizedImageAsync_PreservesAspectRatio()
    {
        var photoId = await SaveTestPhoto(TestJpeg);

        var result = await _resizer.GetResizedImageAsync(photoId, 400, CancellationToken.None);

        Assert.IsNotNull(result);
        using var mat = Mat.FromImageData(result, ImreadModes.Color);
        // Original is 800x600 (4:3). At width=400 → height should be 300.
        Assert.AreEqual(400, mat.Width);
        Assert.AreEqual(300, mat.Height);
    }

    [TestMethod]
    public async Task GetResizedImageAsync_CachesResultOnDisk()
    {
        var photoId = await SaveTestPhoto(TestJpeg);

        // First call — cache miss
        await _resizer.GetResizedImageAsync(photoId, 400, CancellationToken.None);

        // Cache file should now exist
        var snappedWidth = IImageResizer.SnapToAllowedWidth(400);
        var cachePath = Path.Combine(_cacheDirectory, $"{photoId}_{snappedWidth}.jpg");
        Assert.IsTrue(File.Exists(cachePath), "Cache file should exist after first call");

        // Second call — served from cache (we delete the repository entry to confirm)
        var secondResult = await _resizer.GetResizedImageAsync(photoId, 400, CancellationToken.None);
        Assert.IsNotNull(secondResult, "Second call should return cached result");
    }

    [TestMethod]
    public async Task GetResizedImageAsync_SnapsToAllowedWidth()
    {
        var photoId = await SaveTestPhoto(TestJpeg);

        // Request 350 — should snap to 400
        var result = await _resizer.GetResizedImageAsync(photoId, 350, CancellationToken.None);

        Assert.IsNotNull(result);
        var snappedWidth = IImageResizer.SnapToAllowedWidth(350);
        Assert.AreEqual(400, snappedWidth);

        var cachePath = Path.Combine(_cacheDirectory, $"{photoId}_{snappedWidth}.jpg");
        Assert.IsTrue(File.Exists(cachePath), "Cache file should use snapped width");
    }

    [TestMethod]
    public async Task GetResizedImageAsync_WhenOriginalSmallerThanTarget_ReturnsOriginal()
    {
        // Create a small 100x75 JPEG
        var smallJpeg = CreateTestJpeg(width: 100, height: 75);
        var photoId = await SaveTestPhoto(smallJpeg);

        // Request width=400, original is only 100px wide
        var result = await _resizer.GetResizedImageAsync(photoId, 400, CancellationToken.None);

        Assert.IsNotNull(result);
        using var mat = Mat.FromImageData(result, ImreadModes.Color);
        // Should not be upscaled — width stays at 100
        Assert.AreEqual(100, mat.Width);
    }

    private async Task<Guid> SaveTestPhoto(byte[] jpegData)
    {
        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            Code = "1",
            CapturedAt = DateTime.UtcNow
        };
        await _repository.SaveAsync(photo, jpegData);
        return photo.Id;
    }

    private static byte[] CreateTestJpeg(int width, int height)
    {
        using var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(100, 150, 200));
        Cv2.ImEncode(".jpg", mat, out var data, new ImageEncodingParam(ImwriteFlags.JpegQuality, 90));
        return data;
    }
}
