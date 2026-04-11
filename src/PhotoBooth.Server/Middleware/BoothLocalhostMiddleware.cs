using PhotoBooth.Server.Utilities;

namespace PhotoBooth.Server.Middleware;

public sealed class BoothLocalhostMiddleware
{
    private const string ForbiddenHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>403 - Forbidden</title>
            <style>
                body { font-family: system-ui, -apple-system, sans-serif; background: #1a1a1a; color: #fff; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100dvh; margin: 0; }
                .code { font-size: 8rem; font-weight: 700; color: #666; line-height: 1; }
                .message { font-size: 1.25rem; }
            </style>
        </head>
        <body>
            <span class="code">403</span>
            <p class="message">Access restricted to booth display</p>
        </body>
        </html>
        """;

    private readonly RequestDelegate _next;
    private readonly bool _enabled;

    public BoothLocalhostMiddleware(RequestDelegate next, bool enabled)
    {
        _next = next;
        _enabled = enabled;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_enabled && IsRootPath(context.Request.Path) && !NetworkUtilities.IsLocalhost(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(ForbiddenHtml);
            return;
        }

        await _next(context);
    }

    private static bool IsRootPath(PathString path)
    {
        return !path.HasValue || path == "/";
    }
}
