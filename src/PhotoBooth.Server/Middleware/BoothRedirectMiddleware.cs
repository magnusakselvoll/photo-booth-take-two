using PhotoBooth.Server.Utilities;

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
        if (_enabled && IsRootPath(context.Request.Path) && !NetworkUtilities.IsLocalhost(context.Connection.RemoteIpAddress))
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
}
