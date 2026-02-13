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
        // No new file will appear - ListFiles always returns the same set
        _adbService.StaticFileList = new Dictionary<string, int>
        {
            ["existing.jpg"] = 4096
        };

        using var provider = new AndroidCameraProvider(_adbService, _options, _logger);

        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => provider.CaptureAsync());
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
        public Dictionary<string, int>? StaticFileList { get; set; }

        public MockAdbService()
            : base("adb", 10000, NullLogger.Instance)
        {
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
            => Task.FromResult(IsUnlocked ? (true, true) : (false, false));

        public override Task<bool> IsInteractiveAndUnlockedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(IsUnlocked);

        public override Task WakeDeviceAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task UnlockDeviceAsync(string pin, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task OpenCameraAsync(string cameraAction, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task SendFocusAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task TriggerShutterAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override async Task<Dictionary<string, int>> ListFilesAsync(string folder, CancellationToken cancellationToken = default)
        {
            if (SimulateSlowCapture)
            {
                await Task.Delay(30000, cancellationToken);
            }

            return StaticFileList ?? new Dictionary<string, int>();
        }

        public override Task PullFileAsync(string devicePath, string localDir, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task DeleteFileAsync(string devicePath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
