using Microsoft.Extensions.Logging;
using Moq;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class PhotoCaptureServiceTests
{
    private Mock<ICameraProvider> _cameraProviderMock = null!;
    private Mock<IPhotoRepository> _photoRepositoryMock = null!;
    private Mock<IPhotoCodeGenerator> _codeGeneratorMock = null!;
    private Mock<ILogger<PhotoCaptureService>> _loggerMock = null!;
    private PhotoCaptureService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _cameraProviderMock = new Mock<ICameraProvider>();
        _photoRepositoryMock = new Mock<IPhotoRepository>();
        _codeGeneratorMock = new Mock<IPhotoCodeGenerator>();
        _loggerMock = new Mock<ILogger<PhotoCaptureService>>();

        _service = new PhotoCaptureService(
            _cameraProviderMock.Object,
            _photoRepositoryMock.Object,
            _codeGeneratorMock.Object,
            _loggerMock.Object);
    }

    [TestMethod]
    public async Task CaptureAsync_WhenCameraAvailable_ReturnsResult()
    {
        // Arrange
        var imageData = new byte[] { 1, 2, 3 };
        var code = "123456";

        _cameraProviderMock.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cameraProviderMock.Setup(x => x.CaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);
        _codeGeneratorMock.Setup(x => x.GenerateUniqueCodeAsync(It.IsAny<Func<string, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(code);
        _photoRepositoryMock.Setup(x => x.SaveAsync(It.IsAny<Photo>(), imageData, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo p, byte[] _, CancellationToken _) => p);

        // Act
        var result = await _service.CaptureAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(code, result.Code);
        _photoRepositoryMock.Verify(x => x.SaveAsync(It.IsAny<Photo>(), imageData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CaptureAsync_WhenCameraNotAvailable_ThrowsException()
    {
        // Arrange
        _cameraProviderMock.Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

        _photoRepositoryMock.Setup(x => x.GetByCodeAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

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
        _photoRepositoryMock.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

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

        _photoRepositoryMock.Setup(x => x.GetImageDataAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageData);

        // Act
        var result = await _service.GetImageDataAsync(id);

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(imageData, result);
    }
}
