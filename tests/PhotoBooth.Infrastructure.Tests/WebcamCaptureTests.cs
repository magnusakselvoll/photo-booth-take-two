using FlashCap;
using Microsoft.Extensions.Logging;
using PhotoBooth.Infrastructure.Camera;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
public class WebcamCaptureTests
{
    private ILogger<WebcamCameraProvider> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<WebcamCameraProvider>();
    }

    [TestMethod]
    public async Task CaptureMultiplePhotos_ShouldNotCrash()
    {
        var options = new WebcamOptions
        {
            DeviceIndex = 0,
            CaptureLatencyMs = 100,
            FramesToSkip = 5,
            FlipVertical = true,
            PixelOrder = "ARGB",
            JpegQuality = 90
        };

        var provider = new WebcamCameraProvider(_logger, options);

        const int numberOfCaptures = 10;
        var results = new List<(int Index, bool Success, int Bytes, string? Error)>();

        for (var i = 0; i < numberOfCaptures; i++)
        {
            Console.WriteLine($"\n========== CAPTURE {i + 1}/{numberOfCaptures} ==========");

            try
            {
                var startTime = DateTime.Now;
                var imageData = await provider.CaptureAsync();
                var elapsed = DateTime.Now - startTime;

                results.Add((i + 1, true, imageData.Length, null));
                Console.WriteLine($"SUCCESS: Captured {imageData.Length} bytes in {elapsed.TotalMilliseconds:F0}ms");

                // Save the image for manual inspection
                var filename = $"/tmp/webcam_test_{i + 1}.jpg";
                await File.WriteAllBytesAsync(filename, imageData);
                Console.WriteLine($"Saved to {filename}");
            }
            catch (Exception ex)
            {
                results.Add((i + 1, false, 0, ex.Message));
                Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
            }

            // Wait between captures
            Console.WriteLine("Waiting 1 second before next capture...");
            await Task.Delay(1000);
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

        // The test passes if we got at least some captures without crashing
        Assert.IsGreaterThan(0, successCount, "Should capture at least one photo");
    }

    [TestMethod]
    public async Task RawFlashCapCapture_MultiplePhotos()
    {
        // Test FlashCap directly without our wrapper to isolate the issue
        const int numberOfCaptures = 10;

        var devices = new CaptureDevices();
        var descriptors = devices.EnumerateDescriptors().ToList();

        Assert.IsNotEmpty(descriptors, "No camera devices found");

        var descriptor = descriptors[0];
        Console.WriteLine($"Using camera: {descriptor.Name}");

        var characteristics = descriptor.Characteristics
            .Where(c => c.Width >= 640 && c.Height >= 480)
            .OrderByDescending(c => c.Width * c.Height)
            .FirstOrDefault();

        Assert.IsNotNull(characteristics, "No suitable characteristics found");
        Console.WriteLine($"Using: {characteristics.Width}x{characteristics.Height} @ {characteristics.FramesPerSecond}fps, {characteristics.PixelFormat}");

        for (var i = 0; i < numberOfCaptures; i++)
        {
            Console.WriteLine($"\n========== RAW CAPTURE {i + 1}/{numberOfCaptures} ==========");

            try
            {
                var frameReceived = new TaskCompletionSource<byte[]>();
                var frameCount = 0;

                Console.WriteLine("Opening device...");
                var device = await descriptor.OpenAsync(characteristics, bufferScope =>
                {
                    frameCount++;
                    var data = bufferScope.Buffer.ExtractImage();
                    Console.WriteLine($"Frame {frameCount} received ({data.Length} bytes)");

                    if (frameCount == 6 && !frameReceived.Task.IsCompleted) // Skip 5 frames
                    {
                        frameReceived.TrySetResult(data);
                    }
                });

                Console.WriteLine("Starting stream...");
                await device.StartAsync();

                Console.WriteLine("Waiting for frame...");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var completedTask = await Task.WhenAny(
                    frameReceived.Task,
                    Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask == frameReceived.Task)
                {
                    var data = await frameReceived.Task;
                    Console.WriteLine($"Got frame with {data.Length} bytes");
                }
                else
                {
                    Console.WriteLine("TIMEOUT waiting for frame");
                }

                Console.WriteLine("Stopping stream...");
                await device.StopAsync();

                Console.WriteLine("Disposing device...");
                await device.DisposeAsync();

                Console.WriteLine("Waiting 500ms for macOS to release resources...");
                await Task.Delay(500);

                Console.WriteLine($"Capture {i + 1} completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                // Wait longer after an error
                await Task.Delay(2000);
            }
        }
    }

    [TestMethod]
    public async Task RawFlashCapCapture_ReuseDevice()
    {
        // Test if keeping the device open between captures works better
        const int numberOfCaptures = 10;

        var devices = new CaptureDevices();
        var descriptors = devices.EnumerateDescriptors().ToList();

        Assert.IsNotEmpty(descriptors, "No camera devices found");

        var descriptor = descriptors[0];
        Console.WriteLine($"Using camera: {descriptor.Name}");

        var characteristics = descriptor.Characteristics
            .Where(c => c.Width >= 640 && c.Height >= 480)
            .OrderByDescending(c => c.Width * c.Height)
            .FirstOrDefault();

        Assert.IsNotNull(characteristics, "No suitable characteristics found");
        Console.WriteLine($"Using: {characteristics.Width}x{characteristics.Height} @ {characteristics.FramesPerSecond}fps, {characteristics.PixelFormat}");

        byte[]? latestFrame = null;
        var frameCount = 0;
        var captureRequested = false;
        var frameReceived = new TaskCompletionSource<byte[]>();

        Console.WriteLine("Opening device once...");
        var device = await descriptor.OpenAsync(characteristics, bufferScope =>
        {
            frameCount++;

            if (captureRequested && latestFrame is null)
            {
                Console.WriteLine($"Capturing frame {frameCount}");
                latestFrame = bufferScope.Buffer.ExtractImage();
                frameReceived.TrySetResult(latestFrame);
            }
        });

        Console.WriteLine("Starting stream once...");
        await device.StartAsync();

        // Let camera warm up
        Console.WriteLine("Warming up for 1 second...");
        await Task.Delay(1000);

        for (var i = 0; i < numberOfCaptures; i++)
        {
            Console.WriteLine($"\n========== REUSE CAPTURE {i + 1}/{numberOfCaptures} ==========");

            try
            {
                latestFrame = null;
                frameReceived = new TaskCompletionSource<byte[]>();
                captureRequested = true;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var completedTask = await Task.WhenAny(
                    frameReceived.Task,
                    Task.Delay(Timeout.Infinite, cts.Token));

                captureRequested = false;

                if (completedTask == frameReceived.Task)
                {
                    var data = await frameReceived.Task;
                    Console.WriteLine($"Got frame with {data.Length} bytes");

                    var filename = $"/tmp/webcam_reuse_test_{i + 1}.raw";
                    // Note: This is raw data, not JPEG yet
                    await File.WriteAllBytesAsync(filename, data);
                }
                else
                {
                    Console.WriteLine("TIMEOUT");
                }

                // Small delay between captures
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine("\nStopping stream...");
        await device.StopAsync();

        Console.WriteLine("Disposing device...");
        await device.DisposeAsync();
    }
}
