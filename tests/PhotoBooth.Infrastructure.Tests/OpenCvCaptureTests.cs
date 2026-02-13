using Microsoft.Extensions.Logging;
using PhotoBooth.Infrastructure.Camera;

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
[TestCategory("Integration")]
public class OpenCvCaptureTests
{
    private ILogger<OpenCvCameraProvider> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<OpenCvCameraProvider>();
    }

    [TestMethod]
    public async Task CaptureMultiplePhotos_ShouldSucceed()
    {
        var options = new OpenCvCameraOptions
        {
            DeviceIndex = 0,
            CaptureLatencyMs = 100,
            FramesToSkip = 5,
            FlipVertical = false,
            JpegQuality = 90,
            PreferredWidth = 1920,
            PreferredHeight = 1080
        };

        using var provider = new OpenCvCameraProvider(_logger, options);

        // Check availability first
        var isAvailable = await provider.IsAvailableAsync();
        if (!isAvailable)
        {
            Assert.Inconclusive("No camera available for testing");
            return;
        }

        const int numberOfCaptures = 10;
        var results = new List<(int Index, bool Success, int Bytes, string? Error)>();

        for (var i = 0; i < numberOfCaptures; i++)
        {
            Console.WriteLine($"\n========== OPENCV CAPTURE {i + 1}/{numberOfCaptures} ==========");

            try
            {
                var startTime = DateTime.Now;
                var imageData = await provider.CaptureAsync();
                var elapsed = DateTime.Now - startTime;

                results.Add((i + 1, true, imageData.Length, null));
                Console.WriteLine($"SUCCESS: Captured {imageData.Length} bytes in {elapsed.TotalMilliseconds:F0}ms");

                // Verify JPEG format (starts with FFD8FF)
                Assert.IsGreaterThan(3, imageData.Length, "Image data too small");
                Assert.AreEqual(0xFF, imageData[0], "Invalid JPEG header byte 0");
                Assert.AreEqual(0xD8, imageData[1], "Invalid JPEG header byte 1");
                Assert.AreEqual(0xFF, imageData[2], "Invalid JPEG header byte 2");

                // Save the image for manual inspection
                var filename = $"/tmp/opencv_test_{i + 1}.jpg";
                await File.WriteAllBytesAsync(filename, imageData);
                Console.WriteLine($"Saved to {filename}");
            }
            catch (Exception ex)
            {
                results.Add((i + 1, false, 0, ex.Message));
                Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            // Wait between captures
            Console.WriteLine("Waiting 500ms before next capture...");
            await Task.Delay(500);
        }

        // Print summary
        Console.WriteLine("\n========== SUMMARY ==========");
        foreach (var (index, success, bytes, error) in results)
        {
            if (success)
                Console.WriteLine($"  {index}: OK ({bytes} bytes)");
            else
                Console.WriteLine($"  {index}: FAILED - {error}");
        }

        var successCount = results.Count(r => r.Success);
        Console.WriteLine($"\nTotal: {successCount}/{numberOfCaptures} successful");

        // All captures should succeed
        Assert.AreEqual(numberOfCaptures, successCount, "All captures should succeed");
    }

    [TestMethod]
    public async Task IsAvailable_WithValidDevice_ReturnsTrue()
    {
        var options = new OpenCvCameraOptions { DeviceIndex = 0 };
        using var provider = new OpenCvCameraProvider(_logger, options);

        var isAvailable = await provider.IsAvailableAsync();

        // This test assumes a camera is available; skip if not
        if (!isAvailable)
        {
            Assert.Inconclusive("No camera at device index 0");
        }

        Assert.IsTrue(isAvailable);
    }

    [TestMethod]
    public async Task IsAvailable_WithInvalidDevice_ReturnsFalse()
    {
        var options = new OpenCvCameraOptions { DeviceIndex = 99 };
        using var provider = new OpenCvCameraProvider(_logger, options);

        var isAvailable = await provider.IsAvailableAsync();

        Assert.IsFalse(isAvailable, "Device 99 should not exist");
    }

    [TestMethod]
    public async Task CapturedImage_IsValidJpeg()
    {
        var options = new OpenCvCameraOptions
        {
            DeviceIndex = 0,
            JpegQuality = 90
        };
        using var provider = new OpenCvCameraProvider(_logger, options);

        var isAvailable = await provider.IsAvailableAsync();
        if (!isAvailable)
        {
            Assert.Inconclusive("No camera available for testing");
            return;
        }

        var imageData = await provider.CaptureAsync();

        // Verify JPEG magic bytes
        Assert.IsGreaterThan(100, imageData.Length, "Image should have reasonable size");
        Assert.AreEqual(0xFF, imageData[0], "JPEG should start with FF");
        Assert.AreEqual(0xD8, imageData[1], "JPEG should have D8 as second byte");
        Assert.AreEqual(0xFF, imageData[2], "JPEG should have FF as third byte");

        // Verify JPEG end marker
        Assert.AreEqual(0xFF, imageData[^2], "JPEG should end with FFD9");
        Assert.AreEqual(0xD9, imageData[^1], "JPEG should end with FFD9");
    }

    [TestMethod]
    public async Task CaptureLatency_ReturnsConfiguredValue()
    {
        var options = new OpenCvCameraOptions { CaptureLatencyMs = 250 };
        using var provider = new OpenCvCameraProvider(_logger, options);

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), provider.CaptureLatency);
    }
}
