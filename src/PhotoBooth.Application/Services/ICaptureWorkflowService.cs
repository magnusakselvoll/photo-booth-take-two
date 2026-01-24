namespace PhotoBooth.Application.Services;

public interface ICaptureWorkflowService
{
    /// <summary>
    /// Triggers a capture workflow with countdown.
    /// </summary>
    /// <param name="triggerSource">The source that triggered the capture (e.g., "web-ui", "keyboard", "gpio").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the workflow was started, false if a capture is already in progress.</returns>
    Task<bool> TriggerCaptureAsync(string triggerSource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a capture is currently in progress.
    /// </summary>
    bool IsCaptureInProgress { get; }

    /// <summary>
    /// The configured countdown duration in milliseconds.
    /// </summary>
    int CountdownDurationMs { get; }
}
