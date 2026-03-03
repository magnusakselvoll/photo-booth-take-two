using System.Net;

namespace PhotoBooth.Server.Filters;

public class LocalhostOnlyFilter : IEndpointFilter
{
    private readonly bool _enabled;
    private readonly string _endpointName;
    private readonly ILogger<LocalhostOnlyFilter> _logger;

    public LocalhostOnlyFilter(bool enabled, string endpointName, ILogger<LocalhostOnlyFilter> logger)
    {
        _enabled = enabled;
        _endpointName = endpointName;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!_enabled)
        {
            return await next(context);
        }

        var remoteIp = context.HttpContext.Connection.RemoteIpAddress;

        if (!IsLocalhost(remoteIp))
        {
            _logger.LogWarning("Blocked {EndpointName} request from non-localhost IP: {RemoteIp}", _endpointName, remoteIp);
            return Results.Forbid();
        }

        return await next(context);
    }

    private static bool IsLocalhost(IPAddress? ipAddress)
    {
        if (ipAddress is null)
        {
            return false;
        }

        // Check for IPv4 loopback (127.0.0.1)
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
