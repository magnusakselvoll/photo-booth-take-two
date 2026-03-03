using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Infrastructure.Input;

namespace PhotoBooth.Infrastructure.Tests.Input;

[TestClass]
public class InputManagerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task TriggerCaptureAsync_CalledWithCorrectSource_WhenProviderFiresTrigger()
    {
        var provider = new FakeInputProvider("test-source");
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([provider], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await provider.WaitForStartAsync(Timeout);

        provider.FireCaptureTriggered();
        var triggered = await workflow.WaitForTriggerAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.IsTrue(triggered, "Capture should have been triggered");
        Assert.HasCount(1, workflow.TriggerSources);
        Assert.AreEqual("test-source", workflow.TriggerSources[0]);
    }

    [TestMethod]
    public async Task StartsAndStopsCleanly_WhenNoInputProviders()
    {
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await manager.StopAsync(CancellationToken.None);

        Assert.IsEmpty(workflow.TriggerSources);
    }

    [TestMethod]
    public async Task ProvidersStoppedAndUnsubscribed_WhenServiceStopped()
    {
        var provider1 = new FakeInputProvider("p1");
        var provider2 = new FakeInputProvider("p2");
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([provider1, provider2], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await provider1.WaitForStartAsync(Timeout);
        await provider2.WaitForStartAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.IsTrue(provider1.StopCalled, "Provider1 should have been stopped");
        Assert.IsTrue(provider2.StopCalled, "Provider2 should have been stopped");
        Assert.IsFalse(provider1.HasSubscribers, "Provider1 event should be unsubscribed");
        Assert.IsFalse(provider2.HasSubscribers, "Provider2 event should be unsubscribed");

        // Firing after stop should not trigger the workflow
        provider1.FireCaptureTriggered();
        await Task.Delay(100);
        Assert.IsEmpty(workflow.TriggerSources);
    }

    [TestMethod]
    public async Task OtherProvidersStillStarted_WhenOneProviderStartAsyncThrows()
    {
        var failingProvider = new FakeInputProvider("failing")
        {
            ThrowOnStart = new InvalidOperationException("start failed")
        };
        var workingProvider = new FakeInputProvider("working");
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([failingProvider, workingProvider], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await failingProvider.WaitForStartAsync(Timeout);
        await workingProvider.WaitForStartAsync(Timeout);

        Assert.IsTrue(failingProvider.StartCalled, "Failing provider should have been attempted");
        Assert.IsTrue(workingProvider.StartCalled, "Working provider should still have been started");

        workingProvider.FireCaptureTriggered();
        var triggered = await workflow.WaitForTriggerAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.IsTrue(triggered, "Working provider should still be able to trigger captures");
    }

    [TestMethod]
    public async Task OtherProvidersStillStopped_WhenOneProviderStopAsyncThrows()
    {
        var failingProvider = new FakeInputProvider("failing")
        {
            ThrowOnStop = new InvalidOperationException("stop failed")
        };
        var workingProvider = new FakeInputProvider("working");
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([failingProvider, workingProvider], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await failingProvider.WaitForStartAsync(Timeout);
        await workingProvider.WaitForStartAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.IsTrue(failingProvider.StopCalled, "Failing provider stop should have been attempted");
        Assert.IsTrue(workingProvider.StopCalled, "Working provider should still have been stopped");
    }

    [TestMethod]
    public async Task ExceptionLoggedNotRethrown_WhenTriggerCaptureAsyncThrows()
    {
        var provider = new FakeInputProvider("p1");
        var workflow = new FakeCaptureWorkflowService
        {
            ThrowOnTrigger = new InvalidOperationException("workflow blew up")
        };
        var manager = new InputManager([provider], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await provider.WaitForStartAsync(Timeout);

        // Fire once with the failing workflow — should not crash the service
        provider.FireCaptureTriggered();
        await Task.Delay(200);

        // Service should still be alive; now allow successful triggers
        workflow.ThrowOnTrigger = null;
        provider.FireCaptureTriggered();
        var triggered = await workflow.WaitForTriggerAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.IsTrue(triggered, "Service should continue functioning after a swallowed exception");
    }

    [TestMethod]
    public async Task AllProvidersCanTriggerCaptures_WhenMultipleProviders()
    {
        var provider1 = new FakeInputProvider("source-a");
        var provider2 = new FakeInputProvider("source-b");
        var workflow = new FakeCaptureWorkflowService();
        var manager = new InputManager([provider1, provider2], workflow, NullLogger<InputManager>.Instance);

        using var cts = new CancellationTokenSource();
        await manager.StartAsync(cts.Token);
        await provider1.WaitForStartAsync(Timeout);
        await provider2.WaitForStartAsync(Timeout);

        provider1.FireCaptureTriggered();
        await workflow.WaitForTriggerAsync(Timeout);

        provider2.FireCaptureTriggered();
        await workflow.WaitForTriggerAsync(Timeout);

        await manager.StopAsync(CancellationToken.None);

        Assert.HasCount(2, workflow.TriggerSources);
        CollectionAssert.Contains(workflow.TriggerSources, "source-a");
        CollectionAssert.Contains(workflow.TriggerSources, "source-b");
    }
}
