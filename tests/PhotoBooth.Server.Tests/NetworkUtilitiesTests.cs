using System.Net;
using Microsoft.AspNetCore.Http;
using PhotoBooth.Server.Utilities;

namespace PhotoBooth.Server.Tests;

[TestClass]
public sealed class NetworkUtilitiesTests
{
    [TestMethod]
    public void IsLocalhost_WhenNull_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost((IPAddress?)null));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv4Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("127.0.0.1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv6Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("::1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenIPv4MappedIPv6Loopback_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(IPAddress.Parse("::ffff:127.0.0.1")));
    }

    [TestMethod]
    public void IsLocalhost_WhenExternalIPv4_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(IPAddress.Parse("192.168.1.100")));
    }

    [TestMethod]
    public void IsLocalhost_WhenExternalIPv6_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(IPAddress.Parse("2001:db8::1")));
    }

    // Tests for IsLocalhost(HttpContext) overload

    [TestMethod]
    public void IsLocalhostContext_WhenLoopbackIPAndLocalhostHost_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback, "localhost")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenLoopbackIPAnd127Host_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback, "127.0.0.1")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIPv6LoopbackAndIPv6LoopbackHost_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.IPv6Loopback, "::1")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenLoopbackIPAndLocalhostHostWithPort_ReturnsTrue()
    {
        // Request.Host.Host strips the port, so "localhost:5192" → "localhost"
        Assert.IsTrue(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback, "localhost", 5192)));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIPv4MappedLoopbackAndLocalhostHost_ReturnsTrue()
    {
        Assert.IsTrue(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback.MapToIPv6(), "localhost")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenLoopbackIPAndExternalHost_ReturnsFalse()
    {
        // This is the reverse-proxy bug scenario: Tailscale Serve connects from 127.0.0.1
        // but the Host header is the external Tailscale domain
        Assert.IsFalse(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback, "xxx.tailxxxx.ts.net")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenExternalIPAndLocalhostHost_ReturnsFalse()
    {
        // Spoofed Host header — IP check fails first
        Assert.IsFalse(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Parse("192.168.1.100"), "localhost")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenExternalIPAndExternalHost_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Parse("192.168.1.100"), "192.168.1.50")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenNullIPAndLocalhostHost_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(CreateContext(null, "localhost")));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenLoopbackIPAndEmptyHost_ReturnsFalse()
    {
        Assert.IsFalse(NetworkUtilities.IsLocalhost(CreateContext(IPAddress.Loopback, "")));
    }

    // Tests for isRemote query parameter

    [TestMethod]
    public void IsLocalhostContext_WhenIsRemoteTrue_ReturnsFalse()
    {
        var context = CreateContext(IPAddress.Loopback, "localhost", queryString: "?isRemote=true");
        Assert.IsFalse(NetworkUtilities.IsLocalhost(context));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIsRemoteTrueCaseInsensitive_ReturnsFalse()
    {
        var context = CreateContext(IPAddress.Loopback, "localhost", queryString: "?isRemote=TRUE");
        Assert.IsFalse(NetworkUtilities.IsLocalhost(context));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIsRemoteFalseOnLocalhost_ReturnsTrue()
    {
        // isRemote=false is ignored — parameter cannot force localhost
        var context = CreateContext(IPAddress.Loopback, "localhost", queryString: "?isRemote=false");
        Assert.IsTrue(NetworkUtilities.IsLocalhost(context));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIsRemoteFalseOnNonLocalhost_ReturnsFalse()
    {
        // isRemote=false is ignored — external IP still means non-localhost
        var context = CreateContext(IPAddress.Parse("192.168.1.100"), "192.168.1.50", queryString: "?isRemote=false");
        Assert.IsFalse(NetworkUtilities.IsLocalhost(context));
    }

    [TestMethod]
    public void IsLocalhostContext_WhenIsRemoteEmpty_ReturnsNormalBehavior()
    {
        // Empty value is not "true", so normal IP/Host check applies
        var context = CreateContext(IPAddress.Loopback, "localhost", queryString: "?isRemote=");
        Assert.IsTrue(NetworkUtilities.IsLocalhost(context));
    }

    private static HttpContext CreateContext(IPAddress? remoteIp, string host, int? port = null, string? queryString = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIp;
        httpContext.Request.Host = port.HasValue ? new HostString(host, port.Value) : new HostString(host);
        if (queryString != null)
            httpContext.Request.QueryString = new QueryString(queryString);
        return httpContext;
    }
}
