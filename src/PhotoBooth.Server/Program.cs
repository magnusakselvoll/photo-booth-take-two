using PhotoBooth.Application.Events;
using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;
using PhotoBooth.Infrastructure.Camera;
using PhotoBooth.Infrastructure.CodeGeneration;
using PhotoBooth.Infrastructure.Events;
using PhotoBooth.Infrastructure.Input;
using PhotoBooth.Infrastructure.Storage;
using PhotoBooth.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure photo storage path
var configuredPath = builder.Configuration.GetValue<string>("PhotoStorage:Path");
var storagePath = string.IsNullOrWhiteSpace(configuredPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoBooth", "Photos")
    : configuredPath;

// Register event broadcasting
builder.Services.AddSingleton<IEventBroadcaster, EventBroadcaster>();

// Register camera provider
var useMockCamera = builder.Configuration.GetValue<bool>("Camera:UseMock");
var captureLatencyMs = builder.Configuration.GetValue<int?>("Camera:CaptureLatencyMs");
var captureLatency = captureLatencyMs.HasValue
    ? TimeSpan.FromMilliseconds(captureLatencyMs.Value)
    : (TimeSpan?)null;

if (useMockCamera)
{
    builder.Services.AddSingleton<ICameraProvider>(sp =>
        new MockCameraProvider(isAvailable: true, captureLatency: captureLatency));
}
else
{
    var cameraIndex = builder.Configuration.GetValue<int>("Camera:DeviceIndex");
    builder.Services.AddSingleton<ICameraProvider>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<WebcamCameraProvider>>();
        return new WebcamCameraProvider(logger, cameraIndex, captureLatency);
    });
}

builder.Services.AddSingleton<IPhotoRepository>(sp => new FileSystemPhotoRepository(storagePath));
builder.Services.AddSingleton<IPhotoCodeGenerator, NumericCodeGenerator>();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files (for web UI)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

// Map endpoints
app.MapPhotoEndpoints();
app.MapSlideshowEndpoints();
app.MapCameraEndpoints();
app.MapEventsEndpoints();

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
