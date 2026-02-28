using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class PhotoEndpointsTests
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

                    var resizerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IImageResizer));
                    if (resizerDescriptor != null)
                    {
                        services.Remove(resizerDescriptor);
                    }

                    // Add test implementations
                    services.AddSingleton<ICameraProvider>(new MockCameraProvider(isAvailable: true));
                    services.AddSingleton<IPhotoRepository, InMemoryPhotoRepository>();
                    services.AddSingleton<IImageResizer, PassThroughImageResizer>();
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
    public async Task CapturePhoto_ReturnsOkWithCode()
    {
        // Act
        var response = await _client.PostAsync("/api/photos/capture", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureResultDto>();
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.Code));
        Assert.IsTrue(int.TryParse(result.Code, out var code) && code > 0, "Code should be a positive integer");
    }

    [TestMethod]
    public async Task GetPhotoByCode_WhenExists_ReturnsPhoto()
    {
        // Arrange - first capture a photo
        var captureResponse = await _client.PostAsync("/api/photos/capture", null);
        var captureResult = await captureResponse.Content.ReadFromJsonAsync<CaptureResultDto>();

        // Act
        var response = await _client.GetAsync($"/api/photos/{captureResult!.Code}");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photo = await response.Content.ReadFromJsonAsync<PhotoDto>();
        Assert.IsNotNull(photo);
        Assert.AreEqual(captureResult.Code, photo.Code);
    }

    [TestMethod]
    public async Task GetPhotoByCode_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/photos/999999");

        // Assert
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPhotoImage_WhenExists_ReturnsJpeg()
    {
        // Arrange - first capture a photo
        var captureResponse = await _client.PostAsync("/api/photos/capture", null);
        var captureResult = await captureResponse.Content.ReadFromJsonAsync<CaptureResultDto>();

        // Act
        var response = await _client.GetAsync($"/api/photos/{captureResult!.Id}/image");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task GetPhotoImage_WithWidth_ReturnsJpeg()
    {
        // Arrange - first capture a photo
        var captureResponse = await _client.PostAsync("/api/photos/capture", null);
        var captureResult = await captureResponse.Content.ReadFromJsonAsync<CaptureResultDto>();

        // Act
        var response = await _client.GetAsync($"/api/photos/{captureResult!.Id}/image?width=400");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task GetPhotoImage_HasCacheControlHeader()
    {
        // Arrange - first capture a photo
        var captureResponse = await _client.PostAsync("/api/photos/capture", null);
        var captureResult = await captureResponse.Content.ReadFromJsonAsync<CaptureResultDto>();

        // Act
        var response = await _client.GetAsync($"/api/photos/{captureResult!.Id}/image");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var cacheControl = response.Headers.CacheControl;
        Assert.IsNotNull(cacheControl);
        Assert.IsTrue(cacheControl.Public, "Cache-Control should be public");
        Assert.IsTrue(cacheControl.MaxAge.HasValue && cacheControl.MaxAge.Value.TotalSeconds >= 31536000,
            "Cache-Control max-age should be at least 1 year");
    }

    [TestMethod]
    public async Task GetAllPhotos_WhenEmpty_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/photos");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photos = await response.Content.ReadFromJsonAsync<List<PhotoDto>>();
        Assert.IsNotNull(photos);
        Assert.IsEmpty(photos);
    }

    [TestMethod]
    public async Task GetAllPhotos_WhenMultiplePhotos_ReturnsAllOrderedByCodeAscending()
    {
        // Arrange - capture multiple photos
        var firstCaptureResponse = await _client.PostAsync("/api/photos/capture", null);
        var firstCaptureResult = await firstCaptureResponse.Content.ReadFromJsonAsync<CaptureResultDto>();
        await _client.PostAsync("/api/photos/capture", null);
        await _client.PostAsync("/api/photos/capture", null);

        // Act
        var response = await _client.GetAsync("/api/photos");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photos = await response.Content.ReadFromJsonAsync<List<PhotoDto>>();
        Assert.IsNotNull(photos);
        Assert.HasCount(3, photos);
        // Lowest code number should be first (oldest photo)
        Assert.AreEqual(firstCaptureResult!.Code, photos[0].Code);
    }
}

/// <summary>
/// Test implementation that passes through to IPhotoRepository without actual resizing.
/// </summary>
file sealed class PassThroughImageResizer : IImageResizer
{
    private readonly IPhotoRepository _repository;

    public PassThroughImageResizer(IPhotoRepository repository)
    {
        _repository = repository;
    }

    public Task<byte[]?> GetResizedImageAsync(Guid photoId, int width, CancellationToken ct)
        => _repository.GetImageDataAsync(photoId, ct);
}
