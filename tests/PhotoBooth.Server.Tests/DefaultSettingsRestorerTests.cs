using System.Reflection;
using PhotoBooth.Server;

namespace PhotoBooth.Server.Tests;

[TestClass]
public class DefaultSettingsRestorerTests
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
    public void EnsureSettingsExist_WritesFile_WhenMissing()
    {
        var assembly = Assembly.GetAssembly(typeof(DefaultSettingsRestorer))!;

        DefaultSettingsRestorer.EnsureSettingsExist(_tempDir, assembly);

        var settingsPath = Path.Combine(_tempDir, "appsettings.json");
        Assert.IsTrue(File.Exists(settingsPath));

        var content = File.ReadAllText(settingsPath);
        Assert.Contains("Logging", content);
    }

    [TestMethod]
    public void EnsureSettingsExist_DoesNotOverwrite_WhenFileExists()
    {
        var settingsPath = Path.Combine(_tempDir, "appsettings.json");
        var customContent = "{ \"custom\": true }";
        File.WriteAllText(settingsPath, customContent);

        var assembly = Assembly.GetAssembly(typeof(DefaultSettingsRestorer))!;

        DefaultSettingsRestorer.EnsureSettingsExist(_tempDir, assembly);

        var content = File.ReadAllText(settingsPath);
        Assert.AreEqual(customContent, content);
    }
}
