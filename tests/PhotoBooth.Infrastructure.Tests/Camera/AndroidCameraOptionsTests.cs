using PhotoBooth.Infrastructure.Camera;

namespace PhotoBooth.Infrastructure.Tests.Camera;

[TestClass]
public sealed class AndroidCameraOptionsTests
{
    [TestMethod]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        var options = new AndroidCameraOptions();

        options.Validate();
    }

    [TestMethod]
    public void Validate_NumericPin_DoesNotThrow()
    {
        var options = new AndroidCameraOptions { PinCode = "1234" };

        options.Validate();
    }

    [TestMethod]
    public void Validate_NullPin_DoesNotThrow()
    {
        var options = new AndroidCameraOptions { PinCode = null };

        options.Validate();
    }

    [TestMethod]
    [DataRow("12ab")]
    [DataRow("12 34")]
    [DataRow("1234; reboot")]
    public void Validate_NonNumericPin_Throws(string pin)
    {
        var options = new AndroidCameraOptions { PinCode = pin };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate());
        Assert.Contains("PinCode", ex.Message);
    }

    [TestMethod]
    [DataRow("STILL_IMAGE_CAMERA")]
    [DataRow("IMAGE_CAPTURE")]
    [DataRow("VIDEO_CAMERA2")]
    public void Validate_ValidCameraAction_DoesNotThrow(string action)
    {
        var options = new AndroidCameraOptions { CameraAction = action };

        options.Validate();
    }

    [TestMethod]
    [DataRow("STILL_IMAGE_CAMERA; rm -rf /")]
    [DataRow("$(reboot)")]
    [DataRow("FOO`whoami`")]
    [DataRow("FOO BAR")]
    [DataRow("FOO|BAR")]
    [DataRow("")]
    public void Validate_UnsafeCameraAction_Throws(string action)
    {
        var options = new AndroidCameraOptions { CameraAction = action };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate());
        Assert.Contains("CameraAction", ex.Message);
    }
}
