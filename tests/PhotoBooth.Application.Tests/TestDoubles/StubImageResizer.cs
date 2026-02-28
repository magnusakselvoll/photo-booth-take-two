using PhotoBooth.Application.Services;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubImageResizer : IImageResizer
{
    public Task<byte[]?> GetResizedImageAsync(Guid photoId, int width, CancellationToken ct)
        => Task.FromResult<byte[]?>(null);
}
