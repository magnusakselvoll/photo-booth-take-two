using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Tests.Input;

internal sealed class FakeInputProvider : IInputProvider
{
    private readonly SemaphoreSlim _startSignal = new(0);
    private readonly SemaphoreSlim _stopSignal = new(0);

    public string Name { get; }
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public Exception? ThrowOnStart { get; set; }
    public Exception? ThrowOnStop { get; set; }

    public bool HasSubscribers => CaptureTriggered != null;

    public event EventHandler<CaptureTriggeredEventArgs>? CaptureTriggered;

    public FakeInputProvider(string name = "fake")
    {
        Name = name;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCalled = true;
        _startSignal.Release();
        if (ThrowOnStart != null)
            throw ThrowOnStart;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopCalled = true;
        _stopSignal.Release();
        if (ThrowOnStop != null)
            throw ThrowOnStop;
        return Task.CompletedTask;
    }

    public void FireCaptureTriggered()
    {
        CaptureTriggered?.Invoke(this, new CaptureTriggeredEventArgs { Source = Name });
    }

    /// <summary>
    /// Waits until StartAsync has been called by the InputManager's ExecuteAsync loop.
    /// Since the event subscription happens before StartAsync, this also guarantees
    /// that CaptureTriggered is wired up.
    /// </summary>
    public Task<bool> WaitForStartAsync(TimeSpan timeout) => _startSignal.WaitAsync(timeout);

    public Task<bool> WaitForStopAsync(TimeSpan timeout) => _stopSignal.WaitAsync(timeout);
}
