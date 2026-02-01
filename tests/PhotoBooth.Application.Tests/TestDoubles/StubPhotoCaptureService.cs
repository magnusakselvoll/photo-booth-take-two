using PhotoBooth.Application.DTOs;
using PhotoBooth.Application.Services;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubPhotoCaptureService : IPhotoCaptureService
{
    public bool ShouldThrow { get; set; }
    public Exception ExceptionToThrow { get; set; } = new InvalidOperationException("Test exception");
    public CaptureResultDto ResultToReturn { get; set; } = new(Guid.NewGuid(), "123", DateTime.UtcNow);

    public Task<CaptureResultDto> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(ResultToReturn);
    }

    public Task<PhotoDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult<PhotoDto?>(null);

    public Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    public Task<IReadOnlyList<PhotoDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PhotoDto>>([]);
}
