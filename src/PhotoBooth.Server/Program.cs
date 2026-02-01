using PhotoBooth.Application.Events;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.CodeGeneration;
using PhotoBooth.Infrastructure.Events;
using PhotoBooth.Infrastructure.Input;
using PhotoBooth.Infrastructure.Network;
using PhotoBooth.Infrastructure.Storage;
using PhotoBooth.Server.Endpoints;
using PhotoBooth.Server.Filters;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/photobooth.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

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

    case "opencv":
    default:
        var openCvOptions = new OpenCvCameraOptions
        {
            DeviceIndex = builder.Configuration.GetValue<int>("Camera:DeviceIndex"),
            CaptureLatencyMs = builder.Configuration.GetValue<int?>("Camera:CaptureLatencyMs") ?? 100,
            FramesToSkip = builder.Configuration.GetValue<int?>("Camera:FramesToSkip") ?? 5,
            FlipVertical = builder.Configuration.GetValue<bool?>("Camera:FlipVertical") ?? false,
            JpegQuality = builder.Configuration.GetValue<int?>("Camera:JpegQuality") ?? 90,
            PreferredWidth = builder.Configuration.GetValue<int?>("Camera:PreferredWidth") ?? 1920,
            PreferredHeight = builder.Configuration.GetValue<int?>("Camera:PreferredHeight") ?? 1080,
            InitializationWarmupMs = builder.Configuration.GetValue<int?>("Camera:InitializationWarmupMs") ?? 500
        };
        builder.Services.AddSingleton<ICameraProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenCvCameraProvider>>();
            return new OpenCvCameraProvider(logger, openCvOptions);
        });
        break;
}

builder.Services.AddSingleton<IPhotoRepository>(sp => new FileSystemPhotoRepository(basePath, eventName));
builder.Services.AddSingleton<IPhotoCodeGenerator>(sp =>
{
    var repository = sp.GetRequiredService<IPhotoRepository>();
    return new SequentialCodeGenerator(repository.GetCountAsync);
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
var countdownDurationMs = builder.Configuration.GetValue<int?>("Capture:CountdownDurationMs") ?? 3000;
builder.Services.AddSingleton<ICaptureWorkflowService>(sp =>
{
    var captureService = sp.GetRequiredService<IPhotoCaptureService>();
    var cameraProvider = sp.GetRequiredService<ICameraProvider>();
    var eventBroadcaster = sp.GetRequiredService<IEventBroadcaster>();
    var logger = sp.GetRequiredService<ILogger<CaptureWorkflowService>>();
    return new CaptureWorkflowService(captureService, cameraProvider, eventBroadcaster, logger, countdownDurationMs);
});

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

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
