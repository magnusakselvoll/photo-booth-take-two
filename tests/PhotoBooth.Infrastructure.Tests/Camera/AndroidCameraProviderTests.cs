using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Infrastructure.Camera;

namespace PhotoBooth.Infrastructure.Tests.Camera;

[TestClass]
public sealed class AndroidCameraProviderTests
{
    private MockAdbService _adbService = null!;
    private AndroidCameraOptions _options = null!;
    private ILogger<AndroidCameraProvider> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _adbService = new MockAdbService();
        _options = new AndroidCameraOptions
        {
            CaptureLatencyMs = 3000,
            CaptureTimeoutMs = 5000,
            CapturePollingIntervalMs = 100,
            FileStabilityDelayMs = 50,
            FocusKeepaliveIntervalSeconds = 60
        };
        _logger = NullLogger<AndroidCameraProvider>.Instance;
    }

    [TestMethod]
    public async Task IsAvailableAsync_DeviceConnected_ReturnsTrue()
    {
        _adbService.DeviceConnected = true;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        Assert.IsTrue(await provider.IsAvailableAsync());
    }

    [TestMethod]
    public async Task IsAvailableAsync_NoDevice_ReturnsFalse()
    {
        _adbService.DeviceConnected = false;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        Assert.IsFalse(await provider.IsAvailableAsync());
    }

    [TestMethod]
    public async Task IsAvailableAsync_AdbThrows_ReturnsFalse()
    {
        _adbService.ThrowOnDetect = true;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        Assert.IsFalse(await provider.IsAvailableAsync());
    }

    [TestMethod]
    public void CaptureLatency_ReturnsConfiguredValue()
    {
        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        Assert.AreEqual(TimeSpan.FromMilliseconds(3000), provider.CaptureLatency);
    }

    [TestMethod]
    public async Task CaptureAsync_WhenBusy_ThrowsCameraNotAvailable()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _adbService.SimulateSlowCapture = true;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        // Start first capture (will block on slow ListFiles)
        var firstCapture = provider.CaptureAsync();

        // Second capture should fail with "busy"
        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => provider.CaptureAsync());

        // Cancel the slow capture
        _adbService.SimulateSlowCapture = false;
    }

    [TestMethod]
    public async Task CaptureAsync_NoNewFile_ThrowsTimeout()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _options.MaxCaptureRetries = 0; // No retries for this test
        // No new file will appear - ListFiles always returns the same set
        _adbService.StaticFileList = new Dictionary<string, int>
        {
            ["existing.jpg"] = 4096
        };

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => provider.CaptureAsync());
    }

    [TestMethod]
    public async Task CaptureAsync_FirstAttemptFails_RetrySucceeds()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _adbService.FailShutterOnce = true;
        _adbService.SetupSuccessfulCapture();
        _options.MaxCaptureRetries = 1;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        var result = await provider.CaptureAsync();

        // Should succeed on retry with valid JPEG data
        Assert.AreEqual(0xFF, result[0]);
        Assert.AreEqual(0xD8, result[1]);
        Assert.IsGreaterThanOrEqualTo(2, _adbService.OpenCameraCalled, "Should have re-opened camera during recovery");
    }

    [TestMethod]
    public async Task CaptureAsync_AllAttemptsFail_ThrowsException()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _adbService.AlwaysFailShutter = true;
        _options.MaxCaptureRetries = 1;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => provider.CaptureAsync());
    }

    [TestMethod]
    public async Task PrepareAsync_TriggersCameraSetup()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        await provider.PrepareAsync();

        Assert.AreEqual(1, _adbService.OpenCameraCalled, "PrepareAsync should trigger camera setup");
    }

    [TestMethod]
    public async Task PrepareAsync_WhenSetupFails_DoesNotThrow()
    {
        _adbService.DeviceConnected = false;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        // Should not throw — PrepareAsync swallows exceptions
        await provider.PrepareAsync();
    }

    [TestMethod]
    public async Task FocusKeepalive_StopsAndLocksDevice_AfterMaxDuration()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _adbService.SetupSuccessfulCapture();
        _options.FocusKeepaliveIntervalSeconds = 1;
        _options.FocusKeepaliveMaxDurationSeconds = 1; // Expire after 1 second

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        // Trigger capture to start the keepalive timer
        await provider.CaptureAsync();

        // Wait for keepalive to tick and detect max duration exceeded
        await Task.Delay(2500);

        Assert.IsTrue(_adbService.LockDeviceCalled, "Device should have been locked after keepalive timeout");
    }

    [TestMethod]
    public async Task CaptureAsync_DeviceLockedBetweenCaptures_TriggersFullSetup()
    {
        _adbService.DeviceConnected = true;
        _adbService.IsUnlocked = true;
        _adbService.SetupSuccessfulCapture();
        _options.MaxCaptureRetries = 0;

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        // First capture - establishes camera
        await provider.CaptureAsync();
        var openCountAfterFirst = _adbService.OpenCameraCalled;

        // Simulate device locking between captures. The EnsureCameraReadyAsync check
        // will see IsInteractiveAndUnlockedAsync return false on the first call,
        // triggering full setup. After setup calls wake+unlock, device becomes unlocked.
        _adbService.UnlockAfterNChecks = 3; // First few checks return false, then true
        _adbService.ResetFileListForNewCapture();
        await provider.CaptureAsync();

        Assert.IsGreaterThan(openCountAfterFirst, _adbService.OpenCameraCalled,
            "Should have re-opened camera when device was detected as locked");
    }

    /// <summary>
    /// Mock ADB service for unit testing the AndroidCameraProvider.
    /// </summary>
    private sealed class MockAdbService : AdbService
    {
        public bool DeviceConnected { get; set; }
        public bool IsUnlocked { get; set; }
        public bool ThrowOnDetect { get; set; }
        public bool SimulateSlowCapture { get; set; }
        public bool FailShutterOnce { get; set; }
        public bool AlwaysFailShutter { get; set; }
        public bool FailFocus { get; set; }
        public bool LockDeviceCalled { get; private set; }
        public Dictionary<string, int>? StaticFileList { get; set; }
        public int OpenCameraCalled { get; private set; }
        /// <summary>
        /// When set to N > 0, IsInteractiveAndUnlockedAsync returns false for the
        /// first N calls, then returns true. Resets IsUnlocked to true after N calls.
        /// </summary>
        public int UnlockAfterNChecks { get; set; }

        private bool _shutterTriggered;
        private bool _hasNewFile;
        private byte[]? _jpegData;
        private string? _tempDir;
        private int _unlockCheckCount;

        public MockAdbService()
            : base("adb", 10000, NullLogger.Instance)
        {
        }

        public void SetupSuccessfulCapture()
        {
            // Create a minimal valid JPEG file that will be "pulled"
            _jpegData = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
            _hasNewFile = false;
            _shutterTriggered = false;
        }

        public void ResetFileListForNewCapture()
        {
            _hasNewFile = false;
            _shutterTriggered = false;
        }

        public override Task<(bool Connected, string? DeviceInfo)> TryDetectDeviceAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnDetect)
                throw new CameraNotAvailableException("ADB not found");

            return Task.FromResult(DeviceConnected
                ? (true, (string?)"ABC123 (Pixel_3)")
                : (false, (string?)null));
        }

        public override Task<(bool ScreenOn, bool Unlocked)> GetScreenStateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GetEffectiveUnlocked() ? (true, true) : (false, false));

        public override Task<bool> IsInteractiveAndUnlockedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GetEffectiveUnlocked());

        private bool GetEffectiveUnlocked()
        {
            if (UnlockAfterNChecks > 0)
            {
                _unlockCheckCount++;
                if (_unlockCheckCount >= UnlockAfterNChecks)
                {
                    IsUnlocked = true;
                    UnlockAfterNChecks = 0;
                }
                else
                {
                    return false;
                }
            }

            return IsUnlocked;
        }

        public override Task WakeDeviceAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task UnlockDeviceAsync(string pin, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task OpenCameraAsync(string cameraAction, CancellationToken cancellationToken = default)
        {
            OpenCameraCalled++;
            return Task.CompletedTask;
        }

        public override Task LockDeviceAsync(CancellationToken cancellationToken = default)
        {
            LockDeviceCalled = true;
            return Task.CompletedTask;
        }

        public override Task SendFocusAsync(CancellationToken cancellationToken = default)
        {
            if (FailFocus)
                throw new CameraNotAvailableException("Focus failed - device disconnected");
            return Task.CompletedTask;
        }

        public override Task TriggerShutterAsync(CancellationToken cancellationToken = default)
        {
            if (AlwaysFailShutter)
                throw new CameraNotAvailableException("Shutter failed");

            if (FailShutterOnce)
            {
                FailShutterOnce = false;
                throw new CameraNotAvailableException("Shutter failed (transient)");
            }

            _shutterTriggered = true;
            return Task.CompletedTask;
        }

        public override async Task<Dictionary<string, int>> ListFilesAsync(string folder, CancellationToken cancellationToken = default)
        {
            if (SimulateSlowCapture)
            {
                await Task.Delay(30000, cancellationToken);
            }

            if (StaticFileList != null)
            {
                return StaticFileList;
            }

            // If shutter was triggered, show a new file on the second listing call
            if (_shutterTriggered && !_hasNewFile)
            {
                _hasNewFile = true;
                return new Dictionary<string, int>();
            }

            if (_hasNewFile)
            {
                return new Dictionary<string, int> { ["photo_001.jpg"] = 4096 };
            }

            return new Dictionary<string, int>();
        }

        public override Task PullFileAsync(string devicePath, string localDir, CancellationToken cancellationToken = default)
        {
            if (_jpegData != null)
            {
                _tempDir = localDir;
                var fileName = Path.GetFileName(devicePath);
                var localPath = Path.Combine(localDir, fileName);
                Directory.CreateDirectory(localDir);
                File.WriteAllBytes(localPath, _jpegData);
            }

            return Task.CompletedTask;
        }

        public override Task DeleteFileAsync(string devicePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
