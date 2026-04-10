using PhotoBooth.Server.Utilities;

namespace PhotoBooth.Server.Middleware;

public sealed class BoothRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _enabled;
    private readonly string _urlPrefix;

    public BoothRedirectMiddleware(RequestDelegate next, bool enabled, string urlPrefix)
    {
        _next = next;
        _enabled = enabled;
        _urlPrefix = urlPrefix;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (_enabled && IsRootPath(context.Request.Path) && !NetworkUtilities.IsLocalhost(context))
        {
            context.Response.Redirect($"/{_urlPrefix}/download", permanent: false);
            return Task.CompletedTask;
        }

        return _next(context);
    }

    private static bool IsRootPath(PathString path)
    {
        return !path.HasValue || path == "/";
    }
}
