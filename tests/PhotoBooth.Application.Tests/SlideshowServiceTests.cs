using Microsoft.Extensions.Logging;
using Moq;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class SlideshowServiceTests
{
    private Mock<IPhotoRepository> _photoRepositoryMock = null!;
    private Mock<ILogger<SlideshowService>> _loggerMock = null!;
    private SlideshowService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _photoRepositoryMock = new Mock<IPhotoRepository>();
        _loggerMock = new Mock<ILogger<SlideshowService>>();
        _service = new SlideshowService(_photoRepositoryMock.Object, _loggerMock.Object, "/api/photos");
    }

    [TestMethod]
    public async Task GetNextAsync_WhenPhotosExist_ReturnsRandomPhoto()
    {
        // Arrange
        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            Code = "123456",
            CapturedAt = DateTime.UtcNow
        };

        _photoRepositoryMock.Setup(x => x.GetRandomAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(photo);

        // Act
        var result = await _service.GetNextAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(photo.Id, result.Id);
        Assert.AreEqual(photo.Code, result.Code);
        Assert.AreEqual($"/api/photos/{photo.Id}/image", result.ImageUrl);
    }

    [TestMethod]
    public async Task GetNextAsync_WhenNoPhotos_ReturnsNull()
    {
        // Arrange
        _photoRepositoryMock.Setup(x => x.GetRandomAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Photo?)null);

        // Act
        var result = await _service.GetNextAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetRecentAsync_ReturnsPhotosWithImageUrls()
    {
        // Arrange
        var photos = new List<Photo>
        {
            new() { Id = Guid.NewGuid(), Code = "111111", CapturedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "222222", CapturedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        _photoRepositoryMock.Setup(x => x.GetRecentAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(photos);

        // Act
        var result = await _service.GetRecentAsync(2);

        // Assert
        Assert.HasCount(2, result);
        Assert.AreEqual($"/api/photos/{photos[0].Id}/image", result[0].ImageUrl);
        Assert.AreEqual($"/api/photos/{photos[1].Id}/image", result[1].ImageUrl);
    }
}
