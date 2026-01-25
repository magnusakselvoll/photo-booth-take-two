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

// Configure photo storage path and event name
var configuredPath = builder.Configuration.GetValue<string>("PhotoStorage:Path");
var basePath = string.IsNullOrWhiteSpace(configuredPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoBooth", "Photos")
    : configuredPath;

var eventName = builder.Configuration.GetValue<string>("Event:Name") ?? DateTime.Now.ToString("yyyy-MM-dd");

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

// Map endpoints
app.MapPhotoEndpoints();
app.MapSlideshowEndpoints();
app.MapCameraEndpoints();
app.MapEventsEndpoints();

app.Run();

// Make the implicit Program class public for testing
public partial class Program { }
