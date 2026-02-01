using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Application.Events;
using PhotoBooth.Application.Services;
using PhotoBooth.Application.Tests.TestDoubles;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class CaptureWorkflowServiceTests
{
    private StubPhotoCaptureService _captureService = null!;
    private StubCameraProvider _cameraProvider = null!;
    private StubEventBroadcaster _eventBroadcaster = null!;
    private CaptureWorkflowService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _captureService = new StubPhotoCaptureService();
        _cameraProvider = new StubCameraProvider();
        _eventBroadcaster = new StubEventBroadcaster();

        _service = new CaptureWorkflowService(
            _captureService,
            _cameraProvider,
            _eventBroadcaster,
            NullLogger<CaptureWorkflowService>.Instance,
            countdownDurationMs: 100); // Short countdown for tests
    }

    [TestMethod]
    public async Task TriggerCaptureAsync_BroadcastsCountdownStartedEvent()
    {
        // Arrange
        _cameraProvider.IsAvailable = true;

        // Act
        await _service.TriggerCaptureAsync("test");

        // Assert - countdown event should be broadcast immediately
        Assert.HasCount(1, _eventBroadcaster.BroadcastedEvents);
        var countdownEvent = _eventBroadcaster.BroadcastedEvents[0] as CountdownStartedEvent;
        Assert.IsNotNull(countdownEvent);
        Assert.AreEqual(100, countdownEvent.DurationMs);
        Assert.AreEqual("test", countdownEvent.TriggerSource);
    }

    [TestMethod]
    public async Task TriggerCaptureAsync_AfterCountdown_CapturesPhoto()
    {
        // Arrange
        _cameraProvider.IsAvailable = true;

        // Act
        await _service.TriggerCaptureAsync("test");

        // Wait for the background workflow to complete
        await Task.Delay(500);

        // Assert - should have countdown event and photo captured event
        Assert.IsGreaterThanOrEqualTo(_eventBroadcaster.BroadcastedEvents.Count, 2);
        Assert.IsInstanceOfType<CountdownStartedEvent>(_eventBroadcaster.BroadcastedEvents[0]);
        Assert.IsInstanceOfType<PhotoCapturedEvent>(_eventBroadcaster.BroadcastedEvents[1]);
    }

    [TestMethod]
    public async Task TriggerCaptureAsync_WhenCaptureFails_BroadcastsCaptureFailedEvent()
    {
        // Arrange
        _captureService.ShouldThrow = true;
        _captureService.ExceptionToThrow = new CameraNotAvailableException("Test failure");

        // Act
        await _service.TriggerCaptureAsync("test");

        // Wait for the background workflow to complete
        await Task.Delay(500);

        // Assert - should have countdown event and capture failed event
        Assert.IsGreaterThanOrEqualTo(_eventBroadcaster.BroadcastedEvents.Count, 2);
        Assert.IsInstanceOfType<CountdownStartedEvent>(_eventBroadcaster.BroadcastedEvents[0]);

        var failedEvent = _eventBroadcaster.BroadcastedEvents[1] as CaptureFailedEvent;
        Assert.IsNotNull(failedEvent);
        Assert.AreEqual("Test failure", failedEvent.Error);
    }
}
