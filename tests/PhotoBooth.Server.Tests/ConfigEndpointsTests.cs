using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PhotoBooth.Application.DTOs;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class ConfigEndpointsTests
{
    [TestMethod]
    public async Task GetConfig_ReturnsOkWithDefaultConfig()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ClientConfigDto>();
        Assert.IsNotNull(config);
        Assert.IsTrue(config.SwirlEffect, "SwirlEffect should default to true");
        Assert.IsTrue(string.IsNullOrEmpty(config.QrCodeBaseUrl), "QrCodeBaseUrl should default to empty");
    }

    [TestMethod]
    public async Task GetConfig_ReturnsConfiguredQrCodeBaseUrl()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("QrCode:BaseUrl", "https://photos.example.com");
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var config = await response.Content.ReadFromJsonAsync<ClientConfigDto>();
        Assert.IsNotNull(config);
        Assert.AreEqual("https://photos.example.com", config.QrCodeBaseUrl);
    }
}
