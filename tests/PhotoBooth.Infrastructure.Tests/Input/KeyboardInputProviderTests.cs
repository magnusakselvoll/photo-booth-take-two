using Microsoft.Extensions.Logging.Abstractions;
using PhotoBooth.Infrastructure.Input;

namespace PhotoBooth.Infrastructure.Tests.Input;

[TestClass]
public class KeyboardInputProviderTests
{
    [TestMethod]
    public void Name_ReturnsKeyboard()
    {
        var provider = new KeyboardInputProvider(NullLogger<KeyboardInputProvider>.Instance);

        Assert.AreEqual("keyboard", provider.Name);
    }

    [TestMethod]
    public async Task StartAsync_CompletesWithoutError_WhenConsoleUnavailable()
    {
        // In test/CI environments Console.KeyAvailable throws, so StartAsync returns early.
        var provider = new KeyboardInputProvider(NullLogger<KeyboardInputProvider>.Instance);

        await provider.StartAsync();
        await provider.StopAsync();
    }

    [TestMethod]
    public async Task StartAsync_CalledTwice_CompletesWithoutError()
    {
        var provider = new KeyboardInputProvider(NullLogger<KeyboardInputProvider>.Instance);

        await provider.StartAsync();
        await provider.StartAsync(); // second call should be a no-op
        await provider.StopAsync();
    }

    [TestMethod]
    public async Task StopAsync_CompletesCleanly_WhenNeverStarted()
    {
        var provider = new KeyboardInputProvider(NullLogger<KeyboardInputProvider>.Instance);

        await provider.StopAsync();
    }

    [TestMethod]
    public void Dispose_CompletesCleanly_WithoutStarting()
    {
        var provider = new KeyboardInputProvider(NullLogger<KeyboardInputProvider>.Instance);

        provider.Dispose();
    }
}
