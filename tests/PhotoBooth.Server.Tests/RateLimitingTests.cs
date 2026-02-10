using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class RateLimitingTests
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
    public async Task CaptureEndpoint_Returns429AfterExceedingRateLimit()
    {
        // Send 5 requests (the limit)
        for (var i = 0; i < 5; i++)
        {
            var response = await _client.PostAsync("/api/photos/capture", null);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Request {i + 1} should succeed");
        }

        // The 6th request should be rate-limited
        var limitedResponse = await _client.PostAsync("/api/photos/capture", null);
        Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }
}
