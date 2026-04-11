namespace PhotoBooth.Server.Middleware;

public static class BoothLocalhostExtensions
{
    public static IApplicationBuilder UseBoothLocalhost(this IApplicationBuilder app, bool enabled)
    {
        return app.UseMiddleware<BoothLocalhostMiddleware>(enabled);
    }
}
