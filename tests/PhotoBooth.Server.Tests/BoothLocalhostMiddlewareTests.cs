using System.Net;
using Microsoft.AspNetCore.Http;
using PhotoBooth.Server.Middleware;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class BoothLocalhostMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_WhenDisabled_PassesThroughForNonLocalhostRoot()
    {
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: false);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughLocalhostRoot()
    {
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Loopback, "/");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_Returns403ForNonLocalhostRoot()
    {
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled);
        Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.AreEqual("text/html", context.Response.ContentType);
        var body = await ReadResponseBodyAsync(context);
        Assert.Contains("403", body);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughNonLocalhostOnNonRootPath()
    {
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/abc1234567/download");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_PassesThroughNonLocalhostOnApiPath()
    {
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "/api/photos");

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_Returns403ForNullIpFromRoot()
    {
        // Fail-secure: null IP is treated as non-localhost
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(null, "/");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled);
        Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_Returns403ForReverseProxiedRequestFromRoot()
    {
        // Tailscale Serve / local reverse proxy scenario:
        // RemoteIpAddress is 127.0.0.1 but Host header is an external domain
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Loopback, "/", host: "xxx.tailxxxx.ts.net");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled);
        Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_WhenEnabled_Returns403ForLocalhostWithIsRemoteTrue()
    {
        // isRemote=true forces non-localhost treatment even for real localhost
        var nextCalled = false;
        var middleware = new BoothLocalhostMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, enabled: true);
        var context = CreateContext(IPAddress.Loopback, "/", queryString: "?isRemote=true");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled);
        Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static HttpContext CreateContext(IPAddress? remoteIp, string path, string host = "localhost", string? queryString = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Path = path;
        httpContext.Request.Host = new HostString(host);
        if (queryString != null)
            httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }
}
