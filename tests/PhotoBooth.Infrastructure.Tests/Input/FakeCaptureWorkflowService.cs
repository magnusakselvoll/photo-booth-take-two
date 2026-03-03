using PhotoBooth.Application.Services;

namespace PhotoBooth.Infrastructure.Tests.Input;

internal sealed class FakeCaptureWorkflowService : ICaptureWorkflowService
{
    private readonly SemaphoreSlim _triggerSignal = new(0);

    public List<string> TriggerSources { get; } = [];
    public Exception? ThrowOnTrigger { get; set; }
    public int CountdownDurationMs => 0;

    public Task TriggerCaptureAsync(string triggerSource, int? durationOverrideMs = null, CancellationToken cancellationToken = default)
    {
        if (ThrowOnTrigger != null)
            throw ThrowOnTrigger;

        TriggerSources.Add(triggerSource);
        _triggerSignal.Release();
        return Task.CompletedTask;
    }

    public async Task<bool> WaitForTriggerAsync(TimeSpan timeout)
    {
        return await _triggerSignal.WaitAsync(timeout);
    }
}
