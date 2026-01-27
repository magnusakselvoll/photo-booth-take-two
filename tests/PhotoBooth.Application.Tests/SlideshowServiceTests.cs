using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Application.Services;
using PhotoBooth.Application.Tests.TestDoubles;
using PhotoBooth.Domain.Entities;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class SlideshowServiceTests
{
    private StubPhotoRepository _photoRepository = null!;
    private SlideshowService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _photoRepository = new StubPhotoRepository();
        _service = new SlideshowService(
            _photoRepository,
            NullLogger<SlideshowService>.Instance,
            "/api/photos");
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

        _photoRepository.PhotoToReturnRandom = photo;

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
        _photoRepository.PhotoToReturnRandom = null;

        // Act
        var result = await _service.GetNextAsync();

        // Assert
        Assert.IsNull(result);
    }
}
