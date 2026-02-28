namespace PhotoBooth.Server.Middleware;

public static class BoothRedirectExtensions
{
    public static IApplicationBuilder UseBoothRedirect(this IApplicationBuilder app, bool enabled)
    {
        return app.UseMiddleware<BoothRedirectMiddleware>(enabled);
    }
}
