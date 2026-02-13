using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Infrastructure.Camera;

namespace PhotoBooth.Infrastructure.Tests.Camera;

[TestClass]
public sealed class AdbServiceTests
{
    private TestableAdbService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new TestableAdbService(NullLogger.Instance);
    }

    [TestMethod]
    public async Task TryDetectDeviceAsync_WithAuthorizedDevice_ReturnsConnected()
    {
        _service.SetCommandOutput("devices -l",
        [
            "List of devices attached",
            "ABC123              device product:blueline model:Pixel_3 device:blueline transport_id:2 ",
            ""
        ]);

        var (connected, deviceInfo) = await _service.TryDetectDeviceAsync();

        Assert.IsTrue(connected);
        Assert.IsNotNull(deviceInfo);
        Assert.Contains("ABC123", deviceInfo);
        Assert.Contains("Pixel_3", deviceInfo);
    }

    [TestMethod]
    public async Task TryDetectDeviceAsync_WithUsbField_ReturnsConnected()
    {
        _service.SetCommandOutput("devices -l",
        [
            "List of devices attached",
            "8B2X12CVJ              device usb:1-1 product:blueline model:Pixel_3 device:blueline transport_id:1 ",
            ""
        ]);

        var (connected, deviceInfo) = await _service.TryDetectDeviceAsync();

        Assert.IsTrue(connected);
        Assert.IsNotNull(deviceInfo);
        Assert.Contains("8B2X12CVJ", deviceInfo);
        Assert.Contains("Pixel_3", deviceInfo);
    }

    [TestMethod]
    public async Task TryDetectDeviceAsync_WithUnauthorizedDevice_ReturnsNotConnected()
    {
        _service.SetCommandOutput("devices -l",
        [
            "List of devices attached",
            "ABC123              unauthorized transport_id:2",
            ""
        ]);

        var (connected, deviceInfo) = await _service.TryDetectDeviceAsync();

        Assert.IsFalse(connected);
        Assert.IsNull(deviceInfo);
    }

    [TestMethod]
    public async Task TryDetectDeviceAsync_WithNoDevice_ReturnsNotConnected()
    {
        _service.SetCommandOutput("devices -l",
        [
            "List of devices attached",
            ""
        ]);

        var (connected, deviceInfo) = await _service.TryDetectDeviceAsync();

        Assert.IsFalse(connected);
        Assert.IsNull(deviceInfo);
    }

    [TestMethod]
    public async Task TryDetectDeviceAsync_WithDaemonStartup_ReturnsConnected()
    {
        _service.SetCommandOutput("devices -l",
        [
            "* daemon not running; starting now at tcp:5037",
            "* daemon started successfully",
            "List of devices attached",
            "ABC123              device product:blueline model:Pixel_3 device:blueline transport_id:1 ",
            ""
        ]);

        var (connected, deviceInfo) = await _service.TryDetectDeviceAsync();

        Assert.IsTrue(connected);
        Assert.IsNotNull(deviceInfo);
    }

    [TestMethod]
    public async Task IsInteractiveAndUnlockedAsync_ScreenOnUnlocked_ReturnsTrue()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=ON_UNLOCKED"
        ]);

        var result = await _service.IsInteractiveAndUnlockedAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task IsInteractiveAndUnlockedAsync_ScreenOffLocked_ReturnsFalse()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=OFF_LOCKED"
        ]);

        var result = await _service.IsInteractiveAndUnlockedAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task IsInteractiveAndUnlockedAsync_ScreenOnLocked_ReturnsFalse()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=ON_LOCKED"
        ]);

        var result = await _service.IsInteractiveAndUnlockedAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetScreenStateAsync_OnUnlocked_ReturnsBothTrue()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=ON_UNLOCKED"
        ]);

        var (screenOn, unlocked) = await _service.GetScreenStateAsync();

        Assert.IsTrue(screenOn);
        Assert.IsTrue(unlocked);
    }

    [TestMethod]
    public async Task GetScreenStateAsync_OnLocked_ReturnsOnButLocked()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=ON_LOCKED"
        ]);

        var (screenOn, unlocked) = await _service.GetScreenStateAsync();

        Assert.IsTrue(screenOn);
        Assert.IsFalse(unlocked);
    }

    [TestMethod]
    public async Task GetScreenStateAsync_OffLocked_ReturnsBothFalse()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "    mScreenState=OFF_LOCKED"
        ]);

        var (screenOn, unlocked) = await _service.GetScreenStateAsync();

        Assert.IsFalse(screenOn);
        Assert.IsFalse(unlocked);
    }

    [TestMethod]
    public async Task IsInteractiveAndUnlockedAsync_NoScreenState_ReturnsFalse()
    {
        _service.SetCommandOutput("shell dumpsys nfc",
        [
            "some other output",
            "no screen state here"
        ]);

        var result = await _service.IsInteractiveAndUnlockedAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ListFilesAsync_ParsesBlockSizesCorrectly()
    {
        _service.SetCommandOutput("shell ls -s /sdcard/DCIM/Camera",
        [
            "total 12345",
            "  4096 IMG_20240101_120000.jpg",
            "  8192 IMG_20240101_120001.jpg",
            "     0 .nomedia"
        ]);

        var files = await _service.ListFilesAsync("/sdcard/DCIM/Camera");

        Assert.HasCount(3, files);
        Assert.AreEqual(4096, files["IMG_20240101_120000.jpg"]);
        Assert.AreEqual(8192, files["IMG_20240101_120001.jpg"]);
        Assert.AreEqual(0, files[".nomedia"]);
    }

    [TestMethod]
    public async Task ListFilesAsync_SkipsMalformedLines()
    {
        _service.SetCommandOutput("shell ls -s /sdcard/DCIM/Camera",
        [
            "total 12345",
            "  4096 photo.jpg",
            "this is not a valid line",
            "",
            "  8192 another.jpg"
        ]);

        var files = await _service.ListFilesAsync("/sdcard/DCIM/Camera");

        Assert.HasCount(2, files);
        Assert.IsTrue(files.ContainsKey("photo.jpg"));
        Assert.IsTrue(files.ContainsKey("another.jpg"));
    }

    [TestMethod]
    public async Task ListFilesAsync_EmptyFolder_ReturnsEmptyDictionary()
    {
        _service.SetCommandOutput("shell ls -s /sdcard/DCIM/Camera",
        [
            "total 0"
        ]);

        var files = await _service.ListFilesAsync("/sdcard/DCIM/Camera");

        Assert.IsEmpty(files);
    }

    [TestMethod]
    public async Task OpenCameraAsync_SendsCorrectCommandSequence()
    {
        _service.SetCommandOutput("shell am start -a android.media.action.STILL_IMAGE_CAMERA", []);
        _service.SetCommandOutput("shell input keyevent 4", []);

        await _service.OpenCameraAsync("STILL_IMAGE_CAMERA");

        var commands = _service.ExecutedCommands;
        Assert.HasCount(4, commands);
        Assert.AreEqual("shell am start -a android.media.action.STILL_IMAGE_CAMERA", commands[0]);
        Assert.AreEqual("shell input keyevent 4", commands[1]);
        Assert.AreEqual("shell input keyevent 4", commands[2]);
        Assert.AreEqual("shell am start -a android.media.action.STILL_IMAGE_CAMERA", commands[3]);
    }

    [TestMethod]
    public async Task TriggerShutterAsync_SendsVolumeUpKeyEvent()
    {
        _service.SetCommandOutput("shell input keyevent KEYCODE_VOLUME_UP", []);

        await _service.TriggerShutterAsync();

        Assert.HasCount(1, _service.ExecutedCommands);
        Assert.AreEqual("shell input keyevent KEYCODE_VOLUME_UP", _service.ExecutedCommands[0]);
    }

    [TestMethod]
    public async Task UnlockDeviceAsync_SendsPinAndEnter()
    {
        _service.SetCommandOutput("shell input text 1234", []);
        _service.SetCommandOutput("shell input keyevent 66", []);

        await _service.UnlockDeviceAsync("1234");

        Assert.HasCount(2, _service.ExecutedCommands);
        Assert.AreEqual("shell input text 1234", _service.ExecutedCommands[0]);
        Assert.AreEqual("shell input keyevent 66", _service.ExecutedCommands[1]);
    }

    [TestMethod]
    public async Task PullFileAsync_SuccessfulPull_DoesNotThrow()
    {
        _service.SetCommandOutput("pull /sdcard/DCIM/Camera/photo.jpg /tmp",
        [
            "/sdcard/DCIM/Camera/photo.jpg: 1 file pulled. 4.2 MB/s (2048000 bytes in 0.465s)"
        ]);

        await _service.PullFileAsync("/sdcard/DCIM/Camera/photo.jpg", "/tmp");
    }

    [TestMethod]
    public async Task PullFileAsync_FailedPull_ThrowsCameraNotAvailable()
    {
        _service.SetCommandOutput("pull /sdcard/DCIM/Camera/photo.jpg /tmp",
        [
            "adb: error: failed to stat remote object '/sdcard/DCIM/Camera/photo.jpg': No such file or directory"
        ]);

        await Assert.ThrowsExactlyAsync<CameraNotAvailableException>(
            () => _service.PullFileAsync("/sdcard/DCIM/Camera/photo.jpg", "/tmp"));
    }

    [TestMethod]
    public async Task SendFocusAsync_SendsFocusKeyEvent()
    {
        _service.SetCommandOutput("shell input keyevent KEYCODE_FOCUS", []);

        await _service.SendFocusAsync();

        Assert.HasCount(1, _service.ExecutedCommands);
        Assert.AreEqual("shell input keyevent KEYCODE_FOCUS", _service.ExecutedCommands[0]);
    }

    /// <summary>
    /// Testable subclass that overrides ExecuteAdbCommandAsync to return
    /// predetermined output and track executed commands.
    /// </summary>
    private sealed class TestableAdbService : AdbService
    {
        private readonly Dictionary<string, List<string>> _commandOutputs = new();

        public List<string> ExecutedCommands { get; } = [];

        public TestableAdbService(ILogger logger)
            : base("adb", 10000, logger)
        {
        }

        public void SetCommandOutput(string arguments, List<string> output)
        {
            _commandOutputs[arguments] = output;
        }

        protected override Task<List<string>> ExecuteAdbCommandAsync(string arguments, CancellationToken cancellationToken = default)
        {
            ExecutedCommands.Add(arguments);

            if (_commandOutputs.TryGetValue(arguments, out var output))
            {
                return Task.FromResult(output);
            }

            return Task.FromResult(new List<string>());
        }
    }
}
