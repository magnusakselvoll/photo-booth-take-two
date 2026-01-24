using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Input;

/// <summary>
/// Keyboard input provider that listens for a specific key to trigger capture.
/// Works in console environments where Console.ReadKey is available.
/// </summary>
public class KeyboardInputProvider : IInputProvider, IDisposable
{
    private readonly ILogger<KeyboardInputProvider> _logger;
    private readonly ConsoleKey _triggerKey;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public string Name => "keyboard";

    public event EventHandler<CaptureTriggeredEventArgs>? CaptureTriggered;

    public KeyboardInputProvider(ILogger<KeyboardInputProvider> logger, ConsoleKey triggerKey = ConsoleKey.Spacebar)
    {
        _logger = logger;
        _triggerKey = triggerKey;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listenTask != null)
        {
            _logger.LogWarning("Keyboard input provider already running");
            return Task.CompletedTask;
        }

        // Check if we can read from console
        if (!CanReadFromConsole())
        {
            _logger.LogWarning("Console input not available - keyboard input provider will not function");
            return Task.CompletedTask;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = Task.Run(() => ListenForKeyPress(_cts.Token), _cts.Token);

        _logger.LogInformation("Keyboard input provider started. Press {Key} to trigger capture", _triggerKey);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        if (_listenTask != null)
        {
            try
            {
                await _listenTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            _listenTask = null;
        }

        _logger.LogInformation("Keyboard input provider stopped");
    }

    private void ListenForKeyPress(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == _triggerKey)
                    {
                        _logger.LogInformation("Capture triggered by keyboard ({Key})", _triggerKey);
                        CaptureTriggered?.Invoke(this, new CaptureTriggeredEventArgs
                        {
                            Source = Name
                        });
                    }
                }
                else
                {
                    Thread.Sleep(50); // Avoid busy-waiting
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error reading keyboard input");
                Thread.Sleep(1000); // Back off on errors
            }
        }
    }

    private static bool CanReadFromConsole()
    {
        try
        {
            // This will throw if console is not available
            _ = Console.KeyAvailable;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
