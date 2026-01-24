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
    private readonly SemaphoreSlim _captureLock = new(1, 1);

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
        if (!await _captureLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Capture already in progress, ignoring trigger from {Source}", triggerSource);
            return false;
        }

        try
        {
            _isCaptureInProgress = true;
            _logger.LogInformation("Capture workflow started from {Source}", triggerSource);

            // Broadcast countdown started
            await _eventBroadcaster.BroadcastAsync(
                new CountdownStartedEvent(CountdownDurationMs, triggerSource),
                cancellationToken);

            // Calculate when to actually trigger the capture
            // We subtract the camera latency so the photo is taken when countdown hits 0
            var captureLatencyMs = (int)_cameraProvider.CaptureLatency.TotalMilliseconds;
            var delayMs = Math.Max(0, CountdownDurationMs - captureLatencyMs);

            _logger.LogDebug(
                "Waiting {DelayMs}ms before capture (countdown: {CountdownMs}ms, camera latency: {LatencyMs}ms)",
                delayMs, CountdownDurationMs, captureLatencyMs);

            await Task.Delay(delayMs, cancellationToken);

            // Perform the actual capture
            try
            {
                var result = await _captureService.CaptureAsync(cancellationToken);

                _logger.LogInformation("Photo captured: {Code}", result.Code);

                // Broadcast photo captured
                await _eventBroadcaster.BroadcastAsync(
                    new PhotoCapturedEvent(result.Id, result.Code, $"/api/photos/{result.Id}/image"),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Capture failed");

                await _eventBroadcaster.BroadcastAsync(
                    new CaptureFailedEvent(ex.Message),
                    cancellationToken);

                throw;
            }

            return true;
        }
        finally
        {
            _isCaptureInProgress = false;
            _captureLock.Release();
        }
    }
}
