using Microsoft.Extensions.Logging;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Application.Events;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Services;

public class CaptureWorkflowService : ICaptureWorkflowService
{
    private readonly IPhotoCaptureService _captureService;
    private readonly ICameraProvider _cameraProvider;
    private readonly IEventBroadcaster _eventBroadcaster;
    private readonly ILogger<CaptureWorkflowService> _logger;
    private readonly object _lock = new();

    private volatile bool _isCaptureInProgress;

    public bool IsCaptureInProgress => _isCaptureInProgress;
    public int CountdownDurationMs { get; }

    public CaptureWorkflowService(
        IPhotoCaptureService captureService,
        ICameraProvider cameraProvider,
        IEventBroadcaster eventBroadcaster,
        ILogger<CaptureWorkflowService> logger,
        int countdownDurationMs = 3000)
    {
        _captureService = captureService;
        _cameraProvider = cameraProvider;
        _eventBroadcaster = eventBroadcaster;
        _logger = logger;
        CountdownDurationMs = countdownDurationMs;
    }

    public async Task<bool> TriggerCaptureAsync(string triggerSource, CancellationToken cancellationToken = default)
    {
        // Quick check and set flag atomically
        lock (_lock)
        {
            if (_isCaptureInProgress)
            {
                _logger.LogWarning("Capture already in progress, ignoring trigger from {Source}", triggerSource);
                return false;
            }
            _isCaptureInProgress = true;
        }

        _logger.LogInformation("Capture workflow started from {Source}", triggerSource);

        // Broadcast countdown started immediately
        await _eventBroadcaster.BroadcastAsync(
            new CountdownStartedEvent(CountdownDurationMs, triggerSource),
            CancellationToken.None);

        // Run the capture workflow in the background (fire and forget)
        // This allows the HTTP request to return immediately
        _ = RunCaptureWorkflowAsync(triggerSource);

        return true;
    }

    private async Task RunCaptureWorkflowAsync(string triggerSource)
    {
        // Use a hard timeout that will forcefully complete this task
        // even if the camera hangs on synchronous operations
        const int maxWorkflowTimeoutMs = 12000; // 12 seconds max for entire workflow

        var workflowTask = RunCaptureWorkflowCoreAsync(triggerSource);
        var timeoutTask = Task.Delay(maxWorkflowTimeoutMs);

        var completedTask = await Task.WhenAny(workflowTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _logger.LogError("Capture workflow hard timeout after {TimeoutMs}ms - forcefully resetting state", maxWorkflowTimeoutMs);
            try
            {
                await _eventBroadcaster.BroadcastAsync(
                    new CaptureFailedEvent("Capture timed out"),
                    CancellationToken.None);
            }
            catch
            {
                // Ignore broadcast errors
            }
        }

        // Always reset the flag, even if the camera is still hanging in the background
        lock (_lock)
        {
            _isCaptureInProgress = false;
        }
        _logger.LogInformation("Capture workflow completed, ready for next capture");
    }

    private async Task RunCaptureWorkflowCoreAsync(string triggerSource)
    {
        try
        {
            // Calculate when to actually trigger the capture
            // We subtract the camera latency so the photo is taken when countdown hits 0
            var captureLatencyMs = (int)_cameraProvider.CaptureLatency.TotalMilliseconds;
            var delayMs = Math.Max(0, CountdownDurationMs - captureLatencyMs);

            _logger.LogDebug(
                "Waiting {DelayMs}ms before capture (countdown: {CountdownMs}ms, camera latency: {LatencyMs}ms)",
                delayMs, CountdownDurationMs, captureLatencyMs);

            await Task.Delay(delayMs);

            // Perform the actual capture
            var result = await _captureService.CaptureAsync(CancellationToken.None);

            _logger.LogInformation("Photo captured: {Code}", result.Code);

            // Broadcast photo captured
            await _eventBroadcaster.BroadcastAsync(
                new PhotoCapturedEvent(result.Id, result.Code, $"/api/photos/{result.Id}/image"),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture failed");
            await _eventBroadcaster.BroadcastAsync(
                new CaptureFailedEvent(ex.Message),
                CancellationToken.None);
        }
    }
}
