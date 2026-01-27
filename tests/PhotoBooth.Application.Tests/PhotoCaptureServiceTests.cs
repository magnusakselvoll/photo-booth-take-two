using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Application.Services;
using PhotoBooth.Application.Tests.TestDoubles;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class PhotoCaptureServiceTests
{
    private StubCameraProvider _cameraProvider = null!;
    private StubPhotoRepository _photoRepository = null!;
    private StubPhotoCodeGenerator _codeGenerator = null!;
    private PhotoCaptureService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _cameraProvider = new StubCameraProvider();
        _photoRepository = new StubPhotoRepository();
        _codeGenerator = new StubPhotoCodeGenerator();

        _service = new PhotoCaptureService(
            _cameraProvider,
            _photoRepository,
            _codeGenerator,
            NullLogger<PhotoCaptureService>.Instance);
    }

    [TestMethod]
    public async Task CaptureAsync_WhenCameraAvailable_ReturnsResult()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3 };
        var code = "123456";

        _cameraProvider.IsAvailable = true;
        _cameraProvider.ImageData = imageData;
        _codeGenerator.CodeToReturn = code;

        // Act
        var result = await _service.CaptureAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(code, result.Code);
        Assert.HasCount(1, _photoRepository.SavedPhotos);
    }

    [TestMethod]
    public async Task CaptureAsync_WhenCameraNotAvailable_ThrowsException()
    {
        // Arrange
        _cameraProvider.IsAvailable = false;

        // Act & Assert
        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => _service.CaptureAsync());
    }

    [TestMethod]
    public async Task GetByCodeAsync_WhenPhotoExists_ReturnsDto()
    {
        // Arrange
        var code = "123456";
        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            Code = code,
            CapturedAt = DateTime.UtcNow
        };

        _photoRepository.PhotoToReturnByCode = photo;

        // Act
        var result = await _service.GetByCodeAsync(code);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(code, result.Code);
        Assert.AreEqual(photo.Id, result.Id);
    }

    [TestMethod]
    public async Task GetByCodeAsync_WhenPhotoNotFound_ReturnsNull()
    {
        // Arrange
        _photoRepository.PhotoToReturnByCode = null;

        // Act
        var result = await _service.GetByCodeAsync("999999");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetImageDataAsync_WhenImageExists_ReturnsData()
    {
        // Arrange
        var id = Guid.NewGuid();
        var imageData = new byte[] { 1, 2, 3 };

        _photoRepository.ImageDataToReturn = imageData;

        // Act
        var result = await _service.GetImageDataAsync(id);

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(imageData, result);
    }
}
