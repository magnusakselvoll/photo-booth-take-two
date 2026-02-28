using System.Net;
using Microsoft.AspNetCore.Http;
using PhotoBooth.Server.Middleware;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class BoothRedirectMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_WhenDisabled_PassesThroughForNonLocalhostRoot()
    {
        // Arrange
        var middleware = new BoothRedirectMiddleware(enabled: false, next: _ => Task.CompletedTask);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.AreNotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_RedirectsNonLocalhostFromRoot()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called for non-localhost root request");
        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.AreEqual("/download", context.Response.Headers.Location.ToString());
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughIPv4Localhost()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.Loopback, "/"); // 127.0.0.1

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv4 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughIPv6Localhost()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.IPv6Loopback, "/"); // ::1

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv6 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughIPv4MappedLoopback()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.Loopback.MapToIPv6(), "/"); // ::ffff:127.0.0.1

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv4-mapped IPv6 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughNonLocalhostOnNonRootPath()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/download");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for non-root path");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughNonLocalhostOnApiPath()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/api/photos");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for API path");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_RedirectsNullIpFromRoot()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BoothRedirectMiddleware(enabled: true, next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(null, "/");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called when IP is null (fail-secure)");
        Assert.AreEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    private static HttpContext CreateContext(IPAddress? remoteIp, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Path = path;
        return httpContext;
    }
}
