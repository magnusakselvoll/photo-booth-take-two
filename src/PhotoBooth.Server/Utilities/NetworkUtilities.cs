using System.Net;

namespace PhotoBooth.Server.Utilities;

public static class NetworkUtilities
{
    private static readonly HashSet<string> LocalhostHostNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "[::1]"
    };

    public static bool IsLocalhost(IPAddress? ipAddress)
    {
        if (ipAddress is null)
        {
            return false;
        }

        // Check for IPv4 loopback (127.0.0.1) or IPv6 loopback (::1)
        if (IPAddress.IsLoopback(ipAddress))
        {
            return true;
        }

        // Check for IPv4-mapped IPv6 loopback (::ffff:127.0.0.1)
        if (ipAddress.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ipAddress.MapToIPv4()))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true only when both the remote IP is a loopback address AND the Host header
    /// resolves to a localhost name. This guards against local reverse proxies (e.g. Tailscale
    /// Serve) that connect to Kestrel from 127.0.0.1 on behalf of remote clients.
    /// </summary>
    public static bool IsLocalhost(HttpContext httpContext)
    {
        if (!IsLocalhost(httpContext.Connection.RemoteIpAddress))
        {
            return false;
        }

        var host = httpContext.Request.Host.Host;

        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        return LocalhostHostNames.Contains(host);
    }
}
