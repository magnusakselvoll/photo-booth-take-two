using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
[TestCategory("Integration")]
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

    [TestMethod]
    public async Task GetPhotosPage_ReturnsCorrectPageSizeAndNextCursor()
    {
        // Arrange - capture 5 photos
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/photos/capture", null);
        }

        // Act - request first page of 3
        var response = await _client.GetAsync("/api/photos?limit=3");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        Assert.HasCount(3, page.Photos);
        Assert.IsNotNull(page.NextCursor);
    }

    [TestMethod]
    public async Task GetPhotosPage_SecondPageReturnRemainingPhotos()
    {
        // Arrange - capture 5 photos
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/photos/capture", null);
        }

        // Act - get first page then second page
        var firstPageResponse = await _client.GetAsync("/api/photos?limit=3");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(firstPage?.NextCursor);

        var secondPageResponse = await _client.GetAsync($"/api/photos?limit=3&cursor={firstPage.NextCursor}");
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PhotoPageDto>();

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.IsNotNull(secondPage);
        Assert.HasCount(2, secondPage.Photos);
        Assert.IsNull(secondPage.NextCursor);
    }

    [TestMethod]
    public async Task GetPhotosPage_ReturnsNewestFirst()
    {
        // Arrange - capture 3 photos
        var captures = new List<CaptureResultDto>();
        for (var i = 0; i < 3; i++)
        {
            var res = await _client.PostAsync("/api/photos/capture", null);
            var dto = await res.Content.ReadFromJsonAsync<CaptureResultDto>();
            captures.Add(dto!);
        }

        // Act
        var response = await _client.GetAsync("/api/photos?limit=3");
        var page = await response.Content.ReadFromJsonAsync<PhotoPageDto>();

        // Assert - newest (highest code) should be first
        Assert.IsNotNull(page);
        var codes = page.Photos.Select(p => int.Parse(p.Code)).ToList();
        Assert.IsTrue(codes[0] > codes[1] && codes[1] > codes[2], "Photos should be newest-first (descending code)");
    }

    [TestMethod]
    public async Task GetPhotosPage_WhenFewerPhotosThanLimit_HasNoNextCursor()
    {
        // Arrange - capture 2 photos
        await _client.PostAsync("/api/photos/capture", null);
        await _client.PostAsync("/api/photos/capture", null);

        // Act
        var response = await _client.GetAsync("/api/photos?limit=10");
        var page = await response.Content.ReadFromJsonAsync<PhotoPageDto>();

        // Assert
        Assert.IsNotNull(page);
        Assert.HasCount(2, page.Photos);
        Assert.IsNull(page.NextCursor);
    }

    [TestMethod]
    public async Task CapturePhoto_WhenUnexpectedExceptionThrown_Returns500()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var cameraDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICameraProvider));
                    if (cameraDescriptor != null)
                        services.Remove(cameraDescriptor);

                    var repoDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPhotoRepository));
                    if (repoDescriptor != null)
                        services.Remove(repoDescriptor);

                    var resizerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IImageResizer));
                    if (resizerDescriptor != null)
                        services.Remove(resizerDescriptor);

                    services.AddSingleton<ICameraProvider, ThrowingCameraProvider>();
                    services.AddSingleton<IPhotoRepository, InMemoryPhotoRepository>();
                    services.AddSingleton<IImageResizer, PassThroughImageResizer>();
                });
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/photos/capture", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("An unexpected error occurred", body.GetProperty("title").GetString());
        Assert.AreEqual(500, body.GetProperty("status").GetInt32());
    }
}

file record PhotoPageDto(List<PhotoDto> Photos, string? NextCursor);

file sealed class ThrowingCameraProvider : ICameraProvider
{
    public TimeSpan CaptureLatency => TimeSpan.Zero;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated unexpected camera failure");
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
