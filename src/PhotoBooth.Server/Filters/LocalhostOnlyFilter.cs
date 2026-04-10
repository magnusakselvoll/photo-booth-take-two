using System.Net;
using PhotoBooth.Server.Utilities;

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

        if (!NetworkUtilities.IsLocalhost(context.HttpContext))
        {
            _logger.LogWarning("Blocked {EndpointName} request from non-localhost IP: {RemoteIp}", _endpointName, context.HttpContext.Connection.RemoteIpAddress);
            return Results.Forbid();
        }

        return await next(context);
    }
}
