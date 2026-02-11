using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class CameraEndpointsTests
{
    [TestMethod]
    public async Task GetCameraInfo_WhenAvailable_ReturnsInfo()
    {
        using var factory = CreateFactory(new MockCameraProvider(isAvailable: true));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/camera/info");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<CameraInfoDto>();
        Assert.IsNotNull(info);
        Assert.IsTrue(info.IsAvailable);
    }

    [TestMethod]
    public async Task GetCameraInfo_WhenNotAvailable_ReturnsNotAvailable()
    {
        using var factory = CreateFactory(new MockCameraProvider(isAvailable: false));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/camera/info");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<CameraInfoDto>();
        Assert.IsNotNull(info);
        Assert.IsFalse(info.IsAvailable);
    }

    [TestMethod]
    public async Task GetCameraInfo_ReportsConfiguredLatency()
    {
        using var factory = CreateFactory(
            new MockCameraProvider(isAvailable: true, captureLatency: TimeSpan.FromMilliseconds(150)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/camera/info");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<CameraInfoDto>();
        Assert.IsNotNull(info);
        Assert.AreEqual(150, info.CaptureLatencyMs);
    }

    private static WebApplicationFactory<Program> CreateFactory(ICameraProvider cameraProvider)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICameraProvider));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton(cameraProvider);
                });
            });
    }
}
