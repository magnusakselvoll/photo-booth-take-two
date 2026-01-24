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
    public async Task CapturePhoto_ReturnsOkWithCode()
    {
        // Act
        var response = await _client.PostAsync("/api/photos/capture", null);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureResultDto>();
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.Code));
        Assert.AreEqual(6, result.Code.Length);
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
}
