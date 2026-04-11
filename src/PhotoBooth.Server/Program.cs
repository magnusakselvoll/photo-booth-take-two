using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PhotoBooth.Application.Events;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.CodeGeneration;
using PhotoBooth.Infrastructure.Events;
using PhotoBooth.Infrastructure.Imaging;
using PhotoBooth.Infrastructure.Input;
using PhotoBooth.Infrastructure.Monitoring;
using PhotoBooth.Infrastructure.Network;
using PhotoBooth.Infrastructure.Storage;
using PhotoBooth.Server.Endpoints;
using PhotoBooth.Server.Filters;
using PhotoBooth.Server;
using PhotoBooth.Server.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/photobooth.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

DefaultSettingsRestorer.EnsureSettingsExist(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

if (UserSettingsLoader.AddUserSettings(builder.Configuration, AppContext.BaseDirectory))
    Log.Information("Loaded user configuration from {Path}", Path.Combine(AppContext.BaseDirectory, "appsettings.User.json"));

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add network security (blocks outbound HTTP requests by default)
builder.Services.AddNetworkSecurity(builder.Configuration);

// Configure photo storage path and event name
var configuredPath = builder.Configuration.GetValue<string>("PhotoStorage:Path");
var basePath = string.IsNullOrWhiteSpace(configuredPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoBooth", "Photos")
    : configuredPath;

var eventName = builder.Configuration.GetValue<string>("Event:Name") ?? DateTime.Now.ToString("yyyy-MM-dd");
var urlPrefixSalt = builder.Configuration.GetValue<string>("UrlPrefix:Salt") ?? "";
var urlPrefix = UrlPrefixGenerator.Generate(eventName, urlPrefixSalt);
Log.Information("URL prefix for event '{EventName}': {UrlPrefix}", eventName, urlPrefix);

// Register event broadcasting
builder.Services.AddSingleton<IEventBroadcaster, EventBroadcaster>();

// Register camera provider (OpenCV or Mock)
var cameraProvider = builder.Configuration.GetValue<string>("Camera:Provider") ?? "OpenCv";

switch (cameraProvider.ToLowerInvariant())
{
    case "mock":
        var captureLatencyMs = builder.Configuration.GetValue<int?>("Camera:CaptureLatencyMs");
        var captureLatency = captureLatencyMs.HasValue
            ? TimeSpan.FromMilliseconds(captureLatencyMs.Value)
            : (TimeSpan?)null;
        builder.Services.AddSingleton<ICameraProvider>(sp =>
            new MockCameraProvider(isAvailable: true, captureLatency: captureLatency));
        break;

    case "android":
        var androidOptions = new AndroidCameraOptions();
        builder.Configuration.GetSection("Camera:Android").Bind(androidOptions);
        builder.Services.AddSingleton<ICameraProvider>(sp =>
        {
            var adbLogger = sp.GetRequiredService<ILogger<AdbService>>();
            var adbService = new AdbService(androidOptions.AdbPath, androidOptions.AdbCommandTimeoutMs, adbLogger);
            var logger = sp.GetRequiredService<ILogger<AndroidCameraProvider>>();
            return new AndroidCameraProvider(adbService, androidOptions, logger);
        });
        break;

    case "opencv":
    default:
        var openCvOptions = new OpenCvCameraOptions();
        builder.Configuration.GetSection("Camera:OpenCv").Bind(openCvOptions);
        builder.Services.AddSingleton<ICameraProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenCvCameraProvider>>();
            return new OpenCvCameraProvider(logger, openCvOptions);
        });
        break;
}

builder.Services.AddSingleton<IPhotoRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<FileSystemPhotoRepository>>();
    return new FileSystemPhotoRepository(basePath, eventName, logger);
});
builder.Services.AddSingleton<IPhotoCodeGenerator>(sp =>
{
    var repository = sp.GetRequiredService<IPhotoRepository>();
    return new SequentialCodeGenerator(repository.GetCountAsync);
});

// Register image resizer
var thumbnailJpegQuality = builder.Configuration.GetValue<int?>("Thumbnails:JpegQuality") ?? 80;
builder.Services.AddSingleton<IImageResizer>(sp =>
{
    var repository = sp.GetRequiredService<IPhotoRepository>();
    var logger = sp.GetRequiredService<ILogger<OpenCvImageResizer>>();
    return new OpenCvImageResizer(repository, Path.Combine(basePath, ".thumbnails"), thumbnailJpegQuality, logger);
});

// Register application services
builder.Services.AddSingleton<IPhotoCaptureService, PhotoCaptureService>();

// Register capture workflow service
var countdownDurationMs = builder.Configuration.GetValue<int?>("Capture:CountdownDurationMs") ?? 7000;
var bufferTimeoutHighLatencyMs = builder.Configuration.GetValue<int?>("Capture:BufferTimeoutHighLatencyMs") ?? 45000;
var bufferTimeoutLowLatencyMs = builder.Configuration.GetValue<int?>("Capture:BufferTimeoutLowLatencyMs") ?? 12000;
builder.Services.AddSingleton<ICaptureWorkflowService>(sp =>
{
    var captureService = sp.GetRequiredService<IPhotoCaptureService>();
    var cameraProvider = sp.GetRequiredService<ICameraProvider>();
    var eventBroadcaster = sp.GetRequiredService<IEventBroadcaster>();
    var imageResizer = sp.GetRequiredService<IImageResizer>();
    var logger = sp.GetRequiredService<ILogger<CaptureWorkflowService>>();
    var applicationLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    return new CaptureWorkflowService(captureService, cameraProvider, eventBroadcaster, imageResizer, logger, countdownDurationMs, bufferTimeoutHighLatencyMs, bufferTimeoutLowLatencyMs, applicationLifetime.ApplicationStopping);
});

// Register thumbnail warmup service
builder.Services.AddHostedService<ThumbnailWarmupService>();

// Register activity tracker
builder.Services.AddSingleton<IActivityTracker, ActivityTracker>();

// Register inactivity watchdog (disabled if threshold is 0)
var watchdogInactivityMinutes = builder.Configuration.GetValue<int?>("Watchdog:ServerInactivityMinutes") ?? 30;
if (watchdogInactivityMinutes > 0)
{
    builder.Services.AddHostedService<InactivityWatchdogService>();
}

// Register input providers and manager
var enableKeyboardInput = builder.Configuration.GetValue<bool>("Input:EnableKeyboard");
if (enableKeyboardInput)
{
    builder.Services.AddSingleton<IInputProvider, KeyboardInputProvider>();
}
builder.Services.AddHostedService<InputManager>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add rate limiting for capture endpoints
var rateLimitPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 5;
var rateLimitWindowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("capture", limiter =>
    {
        limiter.PermitLimit = rateLimitPermitLimit;
        limiter.Window = TimeSpan.FromSeconds(rateLimitWindowSeconds);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}
else
{
    app.UseHttpsRedirection();
}

// Global exception handler — must be early in the pipeline to catch all downstream exceptions
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (exceptionFeature != null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred", status = 500 });
    });
});

// Security headers (before static files so headers apply to all responses)
app.UseSecurityHeaders();

// Activity tracking (records API calls for inactivity watchdog)
app.UseActivityTracking();

// Rate limiting
app.UseRateLimiter();

// Serve static files (for web UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Log web root for diagnostics (missing index.html causes / to 404)
var webRootPath = app.Environment.WebRootPath;
var indexExists = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;
Log.Information("Web root path: {WebRootPath}, index.html exists: {IndexExists}", webRootPath, indexExists);

// Create localhost-only filter for trigger endpoint
var restrictTriggerToLocalhost = builder.Configuration.GetValue<bool?>("Trigger:RestrictToLocalhost") ?? true;
var triggerLocalhostFilter = new LocalhostOnlyFilter(
    restrictTriggerToLocalhost,
    "trigger",
    app.Services.GetRequiredService<ILogger<LocalhostOnlyFilter>>());

// Create localhost-only filter for capture endpoint
var restrictCaptureToLocalhost = builder.Configuration.GetValue<bool?>("Capture:RestrictToLocalhost") ?? true;
var captureLocalhostFilter = new LocalhostOnlyFilter(
    restrictCaptureToLocalhost,
    "capture",
    app.Services.GetRequiredService<ILogger<LocalhostOnlyFilter>>());

// Create localhost-only filter for camera endpoint
var restrictCameraToLocalhost = builder.Configuration.GetValue<bool?>("Camera:RestrictToLocalhost") ?? true;
var cameraLocalhostFilter = new LocalhostOnlyFilter(
    restrictCameraToLocalhost,
    "camera",
    app.Services.GetRequiredService<ILogger<LocalhostOnlyFilter>>());

// Map endpoints
app.MapPhotoEndpoints(triggerLocalhostFilter, captureLocalhostFilter);
app.MapCameraEndpoints(cameraLocalhostFilter);
app.MapEventsEndpoints();
app.MapConfigEndpoints(builder.Configuration, urlPrefix);

// SPA fallback — only serve index.html for paths under the URL prefix.
// Root / is served by UseDefaultFiles + UseStaticFiles. All other paths return 404.
app.MapFallback($"{urlPrefix}/{{**path}}", async context =>
{
    var fileInfo = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!fileInfo.Exists)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("index.html not found. Has the frontend been built?");
        return;
    }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(fileInfo);
});

// Global catch-all fallback — return a proper 404 page for any unmatched route.
// Without this, ASP.NET Core returns HTTP 200 with an empty body and no Content-Type,
// which causes browsers to offer an empty file download (worsened by X-Content-Type-Options: nosniff).
app.MapFallback(async context =>
{
    // Root path: UseDefaultFiles/UseStaticFiles didn't serve index.html (likely missing from
    // wwwroot), so handle it the same way as the SPA fallback — serve the file or explain
    // what's missing.
    if (context.Request.Path == "/" || !context.Request.Path.HasValue)
    {
        var fileInfo = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        if (fileInfo.Exists)
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(fileInfo);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("index.html not found. Has the frontend been built?");
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync("""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>404 - Not Found</title>
            <style>
                body { font-family: system-ui, -apple-system, sans-serif; background: #1a1a1a; color: #fff; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100dvh; margin: 0; }
                .code { font-size: 8rem; font-weight: 700; color: #666; line-height: 1; }
                .message { font-size: 1.25rem; }
            </style>
        </head>
        <body>
            <span class="code">404</span>
            <p class="message">Page not found</p>
        </body>
        </html>
        """);
});

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
