using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Camera;

/// <summary>
/// Camera provider that uses an Android phone connected via USB/ADB.
/// Triggers the phone's camera shutter via volume-up key event, polls for
/// the new JPEG file, and pulls it via adb pull.
///
/// Based on the patterns from https://github.com/magnusakselvoll/android-photo-booth-camera
/// </summary>
public class AndroidCameraProvider : ICameraProvider, IDisposable
{
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private readonly AdbService _adbService;
    private readonly AndroidCameraOptions _options;
    private readonly ILogger<AndroidCameraProvider> _logger;

    private Timer? _focusTimer;
    private DateTime _lastCameraAction = DateTime.MinValue;
    private DateTime _keepaliveStartedAt;
    private volatile bool _needsRecovery;
    private int _focusTickCount;
    private bool _disposed;

    public TimeSpan CaptureLatency { get; }

    public AndroidCameraProvider(AdbService adbService, AndroidCameraOptions options, ILogger<AndroidCameraProvider> logger)
    {
        _adbService = adbService;
        _options = options;
        _logger = logger;
        CaptureLatency = TimeSpan.FromMilliseconds(options.CaptureLatencyMs);

        _logger.LogInformation(
            "AndroidCameraProvider initialized: adb={AdbPath}, folder={DeviceImageFolder}, latency={CaptureLatencyMs}ms",
            _options.AdbPath,
            _options.DeviceImageFolder,
            _options.CaptureLatencyMs);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (connected, _) = await _adbService.TryDetectDeviceAsync(cancellationToken);
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Android device availability");
            return false;
        }
    }

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!await _captureLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("PrepareAsync skipped — capture already in progress");
            return;
        }

        try
        {
            _logger.LogInformation("Preparing Android camera for upcoming capture");
            await EnsureCameraReadyAsync(cancellationToken);
            _logger.LogInformation("Android camera preparation complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PrepareAsync failed — CaptureAsync will retry setup");
        }
        finally
        {
            _captureLock.Release();
        }
    }

    public async Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Starting Android camera capture");

        if (!await _captureLock.WaitAsync(TimeSpan.FromSeconds(_options.CaptureLockTimeoutSeconds), cancellationToken))
        {
            throw new CameraNotAvailableException("Camera is busy");
        }

        try
        {
            var maxAttempts = 1 + _options.MaxCaptureRetries;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await CaptureWithSetupAsync(cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "Capture attempt {Attempt}/{MaxAttempts} failed, scheduling recovery and retrying",
                        attempt, maxAttempts);

                    _needsRecovery = true;
                    _lastCameraAction = DateTime.MinValue;
                }
            }

            // This is unreachable, but the compiler doesn't know that
            throw new CameraNotAvailableException("Capture failed after all retry attempts");
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private async Task<byte[]> CaptureWithSetupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureCameraReadyAsync(cancellationToken);

            // Snapshot current files on device
            var filesBefore = await _adbService.ListFilesAsync(_options.DeviceImageFolder, cancellationToken);

            // Trigger shutter
            await _adbService.TriggerShutterAsync(cancellationToken);
            UpdateLastCameraAction();

            // Poll for new file
            var newFile = await WaitForNewFileAsync(filesBefore, cancellationToken);
            _logger.LogInformation("New photo detected on device: {FileName}", newFile);

            // Verify file stability
            await VerifyFileStabilityAsync(newFile, cancellationToken);

            // Pull file to local temp directory
            var imageData = await PullAndReadFileAsync(newFile, cancellationToken);

            // Validate JPEG magic bytes
            if (imageData.Length < 3 || imageData[0] != 0xFF || imageData[1] != 0xD8 || imageData[2] != 0xFF)
            {
                throw new CameraNotAvailableException("Downloaded file is not a valid JPEG");
            }

            _logger.LogInformation("Successfully captured Android photo: {Size} bytes", imageData.Length);
            return imageData;
        }
        catch (CameraNotAvailableException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new CameraNotAvailableException("Android camera capture was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture image from Android device");
            throw new CameraNotAvailableException("Failed to capture image from Android device", ex);
        }
    }

    /// <summary>
    /// Ensures the device is awake, unlocked, and the camera app is open.
    /// Re-checks device state before every capture (following the reference implementation pattern)
    /// rather than relying on a persistent initialization flag.
    /// </summary>
    private async Task EnsureCameraReadyAsync(CancellationToken cancellationToken)
    {
        var cameraStale = _lastCameraAction + TimeSpan.FromSeconds(_options.CameraOpenTimeoutSeconds) < DateTime.UtcNow;
        var recoveryNeeded = _needsRecovery;
        var needsFullSetup = cameraStale || recoveryNeeded || !await _adbService.IsInteractiveAndUnlockedAsync(cancellationToken);

        if (!needsFullSetup)
        {
            return;
        }

        _logger.LogInformation("Camera needs setup (stale={CameraStale}, recovery={RecoveryNeeded}), preparing device...",
            cameraStale, recoveryNeeded);

        // Verify device is connected
        var (connected, deviceInfo) = await _adbService.TryDetectDeviceAsync(cancellationToken);
        if (!connected)
        {
            throw new CameraNotAvailableException("No authorized Android device connected");
        }

        _logger.LogInformation("Connected to device: {DeviceInfo}", deviceInfo);

        // Wake screen with retry (reference: up to 5 retries, 200ms apart)
        if (!await _adbService.IsInteractiveAndUnlockedAsync(cancellationToken))
        {
            await WakeDeviceWithRetryAsync(cancellationToken);
            await UnlockDeviceWithRetryAsync(cancellationToken);
        }

        // Open camera app
        await _adbService.OpenCameraAsync(_options.CameraAction, cancellationToken);
        await Task.Delay(1000, cancellationToken);

        // Start/restart focus keepalive timer
        StartFocusKeepalive();

        _needsRecovery = false;
        UpdateLastCameraAction();
        _logger.LogInformation("Android camera ready");
    }

    /// <summary>
    /// Wakes the device screen and retries up to 5 times to verify it's interactive.
    /// Follows the reference implementation's retry pattern.
    /// </summary>
    private async Task WakeDeviceWithRetryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waking device screen...");
        await _adbService.WakeDeviceAsync(cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Task.Delay(200, cancellationToken);

            if (await IsScreenOnAsync(cancellationToken))
            {
                _logger.LogInformation("Device screen is on after {Attempts} check(s)", attempt + 1);
                return;
            }
        }

        _logger.LogWarning("Device screen may not be on after retries, proceeding anyway");
    }

    /// <summary>
    /// Unlocks the device with PIN and retries up to 10 times to verify it's unlocked.
    /// Follows the reference implementation's retry pattern.
    /// </summary>
    private async Task UnlockDeviceWithRetryAsync(CancellationToken cancellationToken)
    {
        if (_options.PinCode is null)
        {
            // No PIN configured — check if device is locked
            if (!await _adbService.IsInteractiveAndUnlockedAsync(cancellationToken))
            {
                _logger.LogWarning("Device is locked but no PIN code is configured");
            }
            return;
        }

        if (await _adbService.IsInteractiveAndUnlockedAsync(cancellationToken))
        {
            return;
        }

        _logger.LogInformation("Unlocking device with PIN...");
        await _adbService.UnlockDeviceAsync(_options.PinCode, cancellationToken);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(200, cancellationToken);

            if (await _adbService.IsInteractiveAndUnlockedAsync(cancellationToken))
            {
                _logger.LogInformation("Device unlocked after {Attempts} check(s)", attempt + 1);
                return;
            }
        }

        throw new CameraNotAvailableException("Unable to unlock device after retries. Is the PIN code correct?");
    }

    /// <summary>
    /// Checks if the screen is on (may be locked or unlocked).
    /// </summary>
    private async Task<bool> IsScreenOnAsync(CancellationToken cancellationToken)
    {
        var (screenOn, _) = await _adbService.GetScreenStateAsync(cancellationToken);
        return screenOn;
    }

    private void UpdateLastCameraAction()
    {
        _lastCameraAction = DateTime.UtcNow;
    }

    private void StartFocusKeepalive()
    {
        StopFocusKeepalive();
        _keepaliveStartedAt = DateTime.UtcNow;
        var interval = TimeSpan.FromSeconds(_options.FocusKeepaliveIntervalSeconds);
        _focusTimer = new Timer(
            _ => _ = SendFocusFireAndForgetAsync(),
            null,
            interval,
            interval);
    }

    private void StopFocusKeepalive()
    {
        _focusTimer?.Dispose();
        _focusTimer = null;
    }

    private async Task SendFocusFireAndForgetAsync()
    {
        try
        {
            // Check if keepalive has exceeded max duration
            if (_options.FocusKeepaliveMaxDurationSeconds > 0)
            {
                var elapsed = DateTime.UtcNow - _keepaliveStartedAt;
                if (elapsed.TotalSeconds >= _options.FocusKeepaliveMaxDurationSeconds)
                {
                    _logger.LogInformation(
                        "Focus keepalive max duration reached ({MaxDuration}s), stopping keepalive and locking device",
                        _options.FocusKeepaliveMaxDurationSeconds);

                    StopFocusKeepalive();
                    await LockDeviceAsync();

                    _needsRecovery = true;
                    _lastCameraAction = DateTime.MinValue;
                    return;
                }
            }

            // Every other tick, verify device is still interactive and unlocked
            var tickCount = Interlocked.Increment(ref _focusTickCount);
            if (tickCount % 2 == 0)
            {
                if (!await _adbService.IsInteractiveAndUnlockedAsync())
                {
                    _logger.LogWarning("Device is no longer interactive/unlocked, flagging recovery needed");
                    _needsRecovery = true;
                    return;
                }
            }

            await _adbService.SendFocusAsync();
            UpdateLastCameraAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Focus keepalive failed, flagging recovery needed");
            _needsRecovery = true;
        }
    }

    private async Task LockDeviceAsync()
    {
        try
        {
            await _adbService.LockDeviceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to lock device");
        }
    }

    private async Task<string> WaitForNewFileAsync(Dictionary<string, int> filesBefore, CancellationToken cancellationToken)
    {
        var matchRegex = new Regex(_options.FileSelectionRegex, RegexOptions.IgnoreCase);
        var timeout = TimeSpan.FromMilliseconds(_options.CaptureTimeoutMs);
        var pollingInterval = TimeSpan.FromMilliseconds(_options.CapturePollingIntervalMs);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(pollingInterval, cancellationToken);

            var filesNow = await _adbService.ListFilesAsync(_options.DeviceImageFolder, cancellationToken);

            foreach (var (fileName, blocks) in filesNow)
            {
                if (!filesBefore.ContainsKey(fileName) && matchRegex.IsMatch(fileName) && blocks > 0)
                {
                    return fileName;
                }
            }
        }

        throw new CameraNotAvailableException(
            $"No new photo appeared on device within {_options.CaptureTimeoutMs}ms timeout");
    }

    private async Task VerifyFileStabilityAsync(string fileName, CancellationToken cancellationToken)
    {
        var firstListing = await _adbService.ListFilesAsync(_options.DeviceImageFolder, cancellationToken);
        await Task.Delay(_options.FileStabilityDelayMs, cancellationToken);
        var secondListing = await _adbService.ListFilesAsync(_options.DeviceImageFolder, cancellationToken);

        if (!firstListing.TryGetValue(fileName, out var firstSize) ||
            !secondListing.TryGetValue(fileName, out var secondSize))
        {
            throw new CameraNotAvailableException($"File {fileName} disappeared during stability check");
        }

        if (firstSize != secondSize)
        {
            _logger.LogWarning("File {FileName} changed size from {First} to {Second} blocks, may still be writing",
                fileName, firstSize, secondSize);

            // Wait once more and check again
            await Task.Delay(_options.FileStabilityDelayMs, cancellationToken);
            var thirdListing = await _adbService.ListFilesAsync(_options.DeviceImageFolder, cancellationToken);

            if (!thirdListing.TryGetValue(fileName, out var thirdSize) || secondSize != thirdSize)
            {
                throw new CameraNotAvailableException($"File {fileName} is still being written to");
            }
        }

        _logger.LogInformation("File {FileName} is stable at {Blocks} blocks", fileName, secondSize);
    }

    private async Task<byte[]> PullAndReadFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var devicePath = $"{_options.DeviceImageFolder.TrimEnd('/')}/{fileName}";
        var tempDir = Path.Combine(Path.GetTempPath(), "photobooth-android");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, fileName);

        try
        {
            await _adbService.PullFileAsync(devicePath, tempDir, cancellationToken);

            if (!File.Exists(localPath))
            {
                throw new CameraNotAvailableException($"File was not downloaded to expected path: {localPath}");
            }

            var imageData = await File.ReadAllBytesAsync(localPath, cancellationToken);

            // Clean up device file if configured
            if (_options.DeleteAfterDownload)
            {
                try
                {
                    await _adbService.DeleteFileAsync(devicePath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file from device: {DevicePath}", devicePath);
                }
            }

            return imageData;
        }
        finally
        {
            // Clean up local temp file
            try
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp file: {LocalPath}", localPath);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopFocusKeepalive();
        _captureLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
