namespace PhotoBooth.Application.Services;

public interface ICaptureWorkflowService
{
    /// <summary>
    /// Triggers a capture workflow with countdown.
    /// Multiple triggers can be active simultaneously.
    /// </summary>
    /// <param name="triggerSource">The source that triggered the capture (e.g., "web-ui", "keyboard", "gpio").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TriggerCaptureAsync(string triggerSource, CancellationToken cancellationToken = default);

    /// <summary>
    /// The configured countdown duration in milliseconds.
    /// </summary>
    int CountdownDurationMs { get; }
}
