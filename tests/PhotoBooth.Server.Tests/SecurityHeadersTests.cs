using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class SecurityHeadersTests
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
    public async Task ApiResponse_ContainsXContentTypeOptionsHeader()
    {
        var response = await _client.GetAsync("/api/photos");

        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [TestMethod]
    public async Task ApiResponse_ContainsXFrameOptionsHeader()
    {
        var response = await _client.GetAsync("/api/photos");

        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [TestMethod]
    public async Task ApiResponse_ContainsReferrerPolicyHeader()
    {
        var response = await _client.GetAsync("/api/photos");

        Assert.AreEqual("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
    }

    [TestMethod]
    public async Task ApiResponse_ContainsPermissionsPolicyHeader()
    {
        var response = await _client.GetAsync("/api/photos");

        Assert.AreEqual(
            "camera=(), microphone=(), geolocation=(), payment=()",
            response.Headers.GetValues("Permissions-Policy").First());
    }

    [TestMethod]
    public async Task ApiResponse_ContainsContentSecurityPolicyHeader()
    {
        var response = await _client.GetAsync("/api/photos");

        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [TestMethod]
    public async Task CaptureResponse_ContainsSecurityHeaders()
    {
        var response = await _client.PostAsync("/api/photos/capture", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(response.Headers.Contains("X-Content-Type-Options"));
        Assert.IsTrue(response.Headers.Contains("X-Frame-Options"));
        Assert.IsTrue(response.Headers.Contains("Content-Security-Policy"));
    }
}
