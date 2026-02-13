using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Exceptions;

namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Encapsulates all ADB (Android Debug Bridge) process execution for Android camera control.
/// Methods are virtual for testability.
/// </summary>
public class AdbService
{
    private readonly string _adbPath;
    private readonly int _commandTimeoutMs;
    private readonly ILogger _logger;

    public AdbService(string adbPath, int commandTimeoutMs, ILogger logger)
    {
        _adbPath = adbPath;
        _commandTimeoutMs = commandTimeoutMs;
        _logger = logger;
    }

    /// <summary>
    /// Detects whether an authorized Android device is connected via ADB.
    /// </summary>
    public virtual async Task<(bool Connected, string? DeviceInfo)> TryDetectDeviceAsync(CancellationToken cancellationToken = default)
    {
        var lines = await ExecuteAdbCommandAsync("devices -l", cancellationToken);

        // The authorized regex allows optional fields (like usb:X-Y) between "device" and "product:"
        var authorizedRegex = new Regex(
            @"^\s*(?<Id>\S+)\s+device\s+.*model:(?<Model>\S+)",
            RegexOptions.IgnoreCase);
        var unauthorizedRegex = new Regex(
            @"^\s*(?<Id>\S+)\s+unauthorized",
            RegexOptions.IgnoreCase);

        foreach (var line in lines)
        {
            var authMatch = authorizedRegex.Match(line);
            if (authMatch.Success)
            {
                var info = $"{authMatch.Groups["Id"].Value} ({authMatch.Groups["Model"].Value})";
                _logger.LogInformation("Detected authorized device: {DeviceInfo}", info);
                return (true, info);
            }

            var unauthMatch = unauthorizedRegex.Match(line);
            if (unauthMatch.Success)
            {
                _logger.LogWarning("Device {DeviceId} is connected but not authorized. Enable USB debugging and authorize this computer.",
                    unauthMatch.Groups["Id"].Value);
                return (false, null);
            }
        }

        _logger.LogWarning("No Android device detected");
        return (false, null);
    }

    /// <summary>
    /// Gets the device screen state from NFC dumpsys.
    /// Returns (screenOn, unlocked) tuple. Both false if state cannot be determined.
    /// Possible states: OFF_LOCKED, OFF_UNLOCKED, ON_LOCKED, ON_UNLOCKED.
    /// </summary>
    public virtual async Task<(bool ScreenOn, bool Unlocked)> GetScreenStateAsync(CancellationToken cancellationToken = default)
    {
        var lines = await ExecuteAdbCommandAsync("shell dumpsys nfc", cancellationToken);

        foreach (var line in lines.Select(x => x.Trim()))
        {
            if (line.StartsWith("mScreenState="))
            {
                var value = line["mScreenState=".Length..].ToUpperInvariant();
                _logger.LogDebug("Device screen state: {ScreenState}", value);

                return value switch
                {
                    "ON_UNLOCKED" => (true, true),
                    "ON_LOCKED" => (true, false),
                    "OFF_UNLOCKED" => (false, true),
                    "OFF_LOCKED" => (false, false),
                    _ => (false, false)
                };
            }
        }

        _logger.LogWarning("Unable to determine device screen state");
        return (false, false);
    }

    /// <summary>
    /// Checks whether the device screen is on and unlocked.
    /// </summary>
    public virtual async Task<bool> IsInteractiveAndUnlockedAsync(CancellationToken cancellationToken = default)
    {
        var (screenOn, unlocked) = await GetScreenStateAsync(cancellationToken);
        return screenOn && unlocked;
    }

    /// <summary>
    /// Wakes the device screen by sending a menu key event.
    /// </summary>
    public virtual async Task WakeDeviceAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAdbCommandAsync("shell input keyevent 82", cancellationToken);
        _logger.LogInformation("Device wake command sent");
    }

    /// <summary>
    /// Unlocks the device by entering a PIN code and pressing Enter.
    /// </summary>
    public virtual async Task UnlockDeviceAsync(string pin, CancellationToken cancellationToken = default)
    {
        await ExecuteAdbCommandAsync($"shell input text {pin}", cancellationToken);
        await Task.Delay(100, cancellationToken);
        await ExecuteAdbCommandAsync("shell input keyevent 66", cancellationToken);
        _logger.LogInformation("Device unlock command sent");
    }

    /// <summary>
    /// Opens the camera app with the back-key reset pattern from the reference implementation.
    /// Opens camera, presses back twice to exit any sub-mode, then opens camera again.
    /// </summary>
    public virtual async Task OpenCameraAsync(string cameraAction, CancellationToken cancellationToken = default)
    {
        await ExecuteAdbCommandAsync($"shell am start -a android.media.action.{cameraAction}", cancellationToken);
        await Task.Delay(500, cancellationToken);
        await ExecuteAdbCommandAsync("shell input keyevent 4", cancellationToken); // back
        await Task.Delay(100, cancellationToken);
        await ExecuteAdbCommandAsync("shell input keyevent 4", cancellationToken); // back
        await Task.Delay(100, cancellationToken);
        await ExecuteAdbCommandAsync($"shell am start -a android.media.action.{cameraAction}", cancellationToken);
        _logger.LogInformation("Camera opened with action {CameraAction}", cameraAction);
    }

    /// <summary>
    /// Sends a focus key event to the camera.
    /// </summary>
    public virtual async Task SendFocusAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAdbCommandAsync("shell input keyevent KEYCODE_FOCUS", cancellationToken);
        _logger.LogDebug("Focus command sent");
    }

    /// <summary>
    /// Triggers the camera shutter via volume-up key event.
    /// </summary>
    public virtual async Task TriggerShutterAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAdbCommandAsync("shell input keyevent KEYCODE_VOLUME_UP", cancellationToken);
        _logger.LogDebug("Shutter triggered");
    }

    /// <summary>
    /// Lists files in a device folder with their block sizes using `ls -s`.
    /// Returns a dictionary mapping filename to block size.
    /// </summary>
    public virtual async Task<Dictionary<string, int>> ListFilesAsync(string folder, CancellationToken cancellationToken = default)
    {
        var lines = await ExecuteAdbCommandAsync($"shell ls -s {folder}", cancellationToken);
        var listing = new Dictionary<string, int>();
        var fileLineRegex = new Regex(@"^\s*(?<Blocks>\d+)\s+(?<Filename>.*\S)\s*");

        foreach (var line in lines)
        {
            var match = fileLineRegex.Match(line);
            if (match.Success)
            {
                listing[match.Groups["Filename"].Value] = int.Parse(match.Groups["Blocks"].Value);
            }
        }

        return listing;
    }

    /// <summary>
    /// Pulls a file from the device to a local directory via `adb pull`.
    /// </summary>
    public virtual async Task PullFileAsync(string devicePath, string localDir, CancellationToken cancellationToken = default)
    {
        var lines = await ExecuteAdbCommandAsync($"pull {devicePath} {localDir}", cancellationToken);

        if (lines.Count == 0 || !lines.Any(l => l.Contains("pulled")))
        {
            throw new CameraNotAvailableException($"Failed to pull file {devicePath}: {lines.FirstOrDefault()}");
        }

        _logger.LogDebug("Pulled file {DevicePath} to {LocalDir}", devicePath, localDir);
    }

    /// <summary>
    /// Deletes a file from the device.
    /// </summary>
    public virtual async Task DeleteFileAsync(string devicePath, CancellationToken cancellationToken = default)
    {
        var lines = await ExecuteAdbCommandAsync($"shell rm {devicePath}", cancellationToken);

        if (lines.Count != 0)
        {
            _logger.LogWarning("Unexpected output when deleting {DevicePath}: {Output}", devicePath, lines[0]);
        }

        _logger.LogDebug("Deleted file {DevicePath} from device", devicePath);
    }

    /// <summary>
    /// Core method for executing ADB commands as external processes with timeout.
    /// </summary>
    protected virtual async Task<List<string>> ExecuteAdbCommandAsync(string arguments, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing ADB command: {Arguments}", arguments);

        var startInfo = new ProcessStartInfo(_adbPath, arguments)
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new CameraNotAvailableException($"Failed to start ADB process at '{_adbPath}'", ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_commandTimeoutMs);

        var lines = new List<string>();

        try
        {
            // Read both stdout and stderr — some ADB commands (e.g., pull) write to stderr
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(timeoutCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0)
                    lines.Add(trimmed);
            }

            foreach (var line in stderr.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.Length > 0)
                    lines.Add(trimmed);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(); } catch { /* best effort */ }
            throw new CameraNotAvailableException($"ADB command timed out after {_commandTimeoutMs}ms: {arguments}");
        }

        return lines;
    }
}
