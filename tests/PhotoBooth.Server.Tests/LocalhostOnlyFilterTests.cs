using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PhotoBooth.Server.Filters;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class LocalhostOnlyFilterTests
{
    private ILogger<LocalhostOnlyFilter> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = LoggerFactory.Create(builder => { }).CreateLogger<LocalhostOnlyFilter>();
    }

    [TestMethod]
    public async Task InvokeAsync_WhenDisabled_AllowsAllRequests()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: false, "test", _logger);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"));
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called when filter is disabled");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_AllowsIPv4Localhost()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.Loopback); // 127.0.0.1
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv4 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_AllowsIPv6Localhost()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.IPv6Loopback); // ::1
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv6 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_AllowsIPv4MappedLoopback()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.Loopback.MapToIPv6()); // ::ffff:127.0.0.1
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsTrue(nextCalled, "Next delegate should be called for IPv4-mapped IPv6 localhost");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_BlocksExternalIPv4()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"));
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called for external IP");
        Assert.IsInstanceOfType<IResult>(result);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_BlocksExternalIPv6()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.Parse("2001:db8::1"));
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called for external IPv6");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_BlocksNullIP()
    {
        // Arrange
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(null);
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called when IP is null (fail-secure)");
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_BlocksReverseProxiedRequest()
    {
        // Arrange — Tailscale Serve / local reverse proxy scenario:
        // RemoteIpAddress is 127.0.0.1 but Host header is an external domain.
        var filter = new LocalhostOnlyFilter(enabled: true, "test", _logger);
        var context = CreateContext(IPAddress.Loopback, host: "xxx.tailxxxx.ts.net");
        var nextCalled = false;

        // Act
        var result = await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.IsFalse(nextCalled, "Next delegate should not be called for reverse-proxied request with external Host");
    }

    private static EndpointFilterInvocationContext CreateContext(IPAddress? remoteIp, string host = "localhost")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Host = new HostString(host);
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}
