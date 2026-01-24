using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class SlideshowEndpointsTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing registrations
                    var cameraDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICameraProvider));
                    if (cameraDescriptor != null)
                    {
                        services.Remove(cameraDescriptor);
                    }

                    var repoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPhotoRepository));
                    if (repoDescriptor != null)
                    {
                        services.Remove(repoDescriptor);
                    }

                    // Add test implementations
                    services.AddSingleton<ICameraProvider>(new MockCameraProvider(isAvailable: true));
                    services.AddSingleton<IPhotoRepository, InMemoryPhotoRepository>();
                });
            });

        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestMethod]
    public async Task GetNextPhoto_WhenNoPhotos_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/slideshow/next");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetNextPhoto_WhenPhotosExist_ReturnsPhoto()
    {
        // Arrange - capture a photo first
        await _client.PostAsync("/api/photos/capture", null);

        // Act
        var response = await _client.GetAsync("/api/slideshow/next");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photo = await response.Content.ReadFromJsonAsync<SlideshowPhotoDto>();
        Assert.IsNotNull(photo);
        Assert.IsNotNull(photo.ImageUrl);
    }

    [TestMethod]
    public async Task GetRecentPhotos_ReturnsEmptyListWhenNoPhotos()
    {
        // Act
        var response = await _client.GetAsync("/api/slideshow/recent?count=5");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photos = await response.Content.ReadFromJsonAsync<List<SlideshowPhotoDto>>();
        Assert.IsNotNull(photos);
        Assert.IsEmpty(photos);
    }

    [TestMethod]
    public async Task GetRecentPhotos_ReturnsPhotosInOrder()
    {
        // Arrange - capture some photos
        await _client.PostAsync("/api/photos/capture", null);
        await _client.PostAsync("/api/photos/capture", null);
        await _client.PostAsync("/api/photos/capture", null);

        // Act
        var response = await _client.GetAsync("/api/slideshow/recent?count=2");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photos = await response.Content.ReadFromJsonAsync<List<SlideshowPhotoDto>>();
        Assert.IsNotNull(photos);
        Assert.HasCount(2, photos);
    }
}
