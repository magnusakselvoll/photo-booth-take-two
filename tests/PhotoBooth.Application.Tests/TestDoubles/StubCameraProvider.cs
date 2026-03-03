using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubCameraProvider : ICameraProvider
{
    public bool IsAvailable { get; set; } = true;
    public byte[] ImageData { get; set; } = [1, 2, 3];
    public TimeSpan CaptureLatency { get; set; } = TimeSpan.Zero;
    public bool PrepareWasCalled { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(IsAvailable);

    public Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        PrepareWasCalled = true;
        return Task.CompletedTask;
    }

    public Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ImageData);
}
