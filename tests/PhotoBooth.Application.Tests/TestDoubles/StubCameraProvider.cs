using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubCameraProvider : ICameraProvider
{
    public bool IsAvailable { get; set; } = true;
    public byte[] ImageData { get; set; } = [1, 2, 3];
    public TimeSpan CaptureLatency { get; set; } = TimeSpan.Zero;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(IsAvailable);

    public Task<byte[]> CaptureAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ImageData);
}
