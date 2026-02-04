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

    public async Task TriggerCaptureAsync(string triggerSource, int? durationOverrideMs = null, CancellationToken cancellationToken = default)
    {
        var effectiveDuration = durationOverrideMs ?? CountdownDurationMs;
        _logger.LogInformation("Capture workflow started from {Source} with duration {Duration}ms", triggerSource, effectiveDuration);

        // Broadcast countdown started immediately
        await _eventBroadcaster.BroadcastAsync(
            new CountdownStartedEvent(effectiveDuration, triggerSource),
            CancellationToken.None);

        // Run the capture workflow in the background (fire and forget)
        // This allows the HTTP request to return immediately
        // Multiple workflows can run in parallel
        _ = RunCaptureWorkflowAsync(triggerSource, effectiveDuration);
    }

    private async Task RunCaptureWorkflowAsync(string triggerSource, int countdownDurationMs)
    {
        // Use a hard timeout that will forcefully complete this task
        // even if the camera hangs on synchronous operations
        const int maxWorkflowTimeoutMs = 12000; // 12 seconds max for entire workflow

        var workflowTask = RunCaptureWorkflowCoreAsync(triggerSource, countdownDurationMs);
        var timeoutTask = Task.Delay(maxWorkflowTimeoutMs);

        var completedTask = await Task.WhenAny(workflowTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _logger.LogError("Capture workflow hard timeout after {TimeoutMs}ms", maxWorkflowTimeoutMs);
            try
            {
                await _eventBroadcaster.BroadcastAsync(
                    new CaptureFailedEvent("Capture timed out"),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast timeout event");
            }
        }

        _logger.LogInformation("Capture workflow completed for trigger from {Source}", triggerSource);
    }

    private async Task RunCaptureWorkflowCoreAsync(string triggerSource, int countdownDurationMs)
    {
        try
        {
            // Calculate when to actually trigger the capture
            // We subtract the camera latency so the photo is taken when countdown hits 0
            var captureLatencyMs = (int)_cameraProvider.CaptureLatency.TotalMilliseconds;
            var delayMs = Math.Max(0, countdownDurationMs - captureLatencyMs);

            _logger.LogDebug(
                "Waiting {DelayMs}ms before capture (countdown: {CountdownMs}ms, camera latency: {LatencyMs}ms)",
                delayMs, countdownDurationMs, captureLatencyMs);

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
