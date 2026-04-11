using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class FallbackRouteTests
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
    public async Task UnmatchedUrl_Returns404()
    {
        var response = await _client.GetAsync("/nonexistent/path");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task UnmatchedUrl_ReturnsHtmlBody()
    {
        var response = await _client.GetAsync("/nonexistent/path");

        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("404", body);
        Assert.Contains("Page not found", body);
    }

    [TestMethod]
    public async Task UnmatchedUrl_ContainsSecurityHeaders()
    {
        var response = await _client.GetAsync("/nonexistent/path");

        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [TestMethod]
    public async Task SpaFallbackWithMissingIndexHtml_Returns404()
    {
        // When index.html is absent (frontend not built), the SPA fallback should
        // return 404 rather than an empty 200 or throw a 500.
        // In the test environment the wwwroot directory is not populated, so this
        // verifies the existence guard added to the SPA MapFallback handler.
        var config = await _client.GetStringAsync("/api/config");
        var urlPrefix = System.Text.Json.JsonDocument.Parse(config)
            .RootElement.GetProperty("urlPrefix").GetString()!;

        var response = await _client.GetAsync($"/{urlPrefix}/photo/1");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task RootPathWithMissingIndexHtml_Returns404WithDiagnosticMessage()
    {
        // When index.html is absent, requesting / should return 404 with a diagnostic
        // plain-text message rather than the generic styled 404 HTML page.
        var response = await _client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("index.html not found", body);
    }
}
