using Microsoft.Extensions.Configuration;
using PhotoBooth.Server;

namespace PhotoBooth.Server.Tests;

[TestClass]
public class UserSettingsLoaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void AddUserSettings_UserFileOverridesBaseFile_WhenBothPresent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"),
            """{"Camera": {"Provider": "OpenCv"}}""");
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.User.json"),
            """{"Camera": {"Provider": "Android"}}""");

        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(_tempDir, "appsettings.json"), optional: false);

        var found = UserSettingsLoader.AddUserSettings(configuration, _tempDir);

        Assert.IsTrue(found);
        Assert.AreEqual("Android", configuration["Camera:Provider"]);
    }

    [TestMethod]
    public void AddUserSettings_ReturnsFalse_WhenUserFileAbsent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"),
            """{"Camera": {"Provider": "OpenCv"}}""");

        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(_tempDir, "appsettings.json"), optional: false);

        var found = UserSettingsLoader.AddUserSettings(configuration, _tempDir);

        Assert.IsFalse(found);
        Assert.AreEqual("OpenCv", configuration["Camera:Provider"]);
    }

    [TestMethod]
    public void AddUserSettings_MergesPartialOverride_LeavingUnchangedKeysIntact()
    {
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"),
            """{"Camera": {"Provider": "OpenCv"}, "Event": {"Name": "Default"}}""");
        File.WriteAllText(Path.Combine(_tempDir, "appsettings.User.json"),
            """{"Event": {"Name": "Wedding2026"}}""");

        var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(_tempDir, "appsettings.json"), optional: false);
        UserSettingsLoader.AddUserSettings(configuration, _tempDir);

        Assert.AreEqual("OpenCv", configuration["Camera:Provider"]);
        Assert.AreEqual("Wedding2026", configuration["Event:Name"]);
    }
}
