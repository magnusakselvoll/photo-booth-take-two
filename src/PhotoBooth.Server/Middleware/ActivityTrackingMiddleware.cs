using PhotoBooth.Application.Services;

namespace PhotoBooth.Server.Middleware;

public sealed class ActivityTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public ActivityTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context, IActivityTracker activityTracker)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/api/events", StringComparison.OrdinalIgnoreCase))
        {
            activityTracker.RecordActivity();
        }

        return _next(context);
    }
}

public static class ActivityTrackingExtensions
{
    public static IApplicationBuilder UseActivityTracking(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ActivityTrackingMiddleware>();
    }
}
