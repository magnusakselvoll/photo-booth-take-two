using System.Net;

namespace PhotoBooth.Server.Middleware;

public sealed class BoothRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _enabled;

    public BoothRedirectMiddleware(RequestDelegate next, bool enabled)
    {
        _next = next;
        _enabled = enabled;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_enabled && IsRootPath(context.Request.Path) && !IsLocalhost(context.Connection.RemoteIpAddress))
        {
            context.Response.Redirect("/download", permanent: false);
            return Task.CompletedTask;
        }

        return _next(context);
    }

    private static bool IsRootPath(PathString path)
    {
        return !path.HasValue || path == "/";
    }

    private static bool IsLocalhost(IPAddress? ipAddress)
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
}
