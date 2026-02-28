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
builder.Services.AddSingleton<ISlideshowService>(sp =>
{
    var repository = sp.GetRequiredService<IPhotoRepository>();
    var logger = sp.GetRequiredService<ILogger<SlideshowService>>();
    return new SlideshowService(repository, logger);
});

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
    return new CaptureWorkflowService(captureService, cameraProvider, eventBroadcaster, imageResizer, logger, countdownDurationMs, bufferTimeoutHighLatencyMs, bufferTimeoutLowLatencyMs);
});

// Register thumbnail warmup service
builder.Services.AddHostedService<ThumbnailWarmupService>();

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

// Security headers (before static files so headers apply to all responses)
app.UseSecurityHeaders();

// Rate limiting
app.UseRateLimiter();

// Serve static files (for web UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Create localhost-only filter for trigger endpoint
var restrictTriggerToLocalhost = builder.Configuration.GetValue<bool?>("Trigger:RestrictToLocalhost") ?? true;
var localhostFilter = new LocalhostOnlyFilter(
    restrictTriggerToLocalhost,
    app.Services.GetRequiredService<ILogger<LocalhostOnlyFilter>>());

// Map endpoints
app.MapPhotoEndpoints(localhostFilter);
app.MapSlideshowEndpoints();
app.MapCameraEndpoints();
app.MapEventsEndpoints();
app.MapConfigEndpoints(builder.Configuration);

// SPA fallback for client-side routing
app.MapFallbackToFile("index.html");

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
