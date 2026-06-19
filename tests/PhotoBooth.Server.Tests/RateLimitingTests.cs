using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
[TestCategory("Integration")]
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

    [TestMethod]
    public async Task CodeLookupEndpoint_Returns429AfterExceedingRateLimit()
    {
        // The lookup limit is 10 per window (rate limiting runs before the handler, so a
        // missing code returns 404 but still consumes the quota).
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.GetAsync($"/api/photos/{i}");
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"Lookup {i + 1} should pass the limiter");
        }

        // The 11th lookup should be rate-limited.
        var limitedResponse = await _client.GetAsync("/api/photos/999");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    [TestMethod]
    public async Task ImageEndpoint_IsNotRateLimited()
    {
        // The bulk export script (scripts/download-event.sh) downloads every photo via this
        // endpoint, so it must not be throttled. Exceed the lookup limit and assert no 429.
        for (var i = 0; i < 15; i++)
        {
            var response = await _client.GetAsync($"/api/photos/{Guid.NewGuid()}/image");
            Assert.AreNotEqual(HttpStatusCode.TooManyRequests, response.StatusCode, $"Image request {i + 1} should not be throttled");
        }
    }

    [TestMethod]
    public async Task ListEndpoint_IsNotRateLimited()
    {
        // The gallery and the export script fetch the full list; it must not be throttled.
        for (var i = 0; i < 15; i++)
        {
            var response = await _client.GetAsync("/api/photos/");
            Assert.AreNotEqual(HttpStatusCode.TooManyRequests, response.StatusCode, $"List request {i + 1} should not be throttled");
        }
    }
}
