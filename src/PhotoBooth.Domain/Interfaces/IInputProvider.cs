namespace PhotoBooth.Domain.Interfaces;

/// <summary>
/// Represents a hardware input device that can trigger photo capture.
/// </summary>
public interface IInputProvider
{
    /// <summary>
    /// Unique name for this input provider (e.g., "keyboard", "gpio-button", "footpedal").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Starts listening for input triggers.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening for input triggers.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a capture is triggered by this input device.
    /// </summary>
    event EventHandler<CaptureTriggeredEventArgs>? CaptureTriggered;
}

public class CaptureTriggeredEventArgs : EventArgs
{
    public required string Source { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
