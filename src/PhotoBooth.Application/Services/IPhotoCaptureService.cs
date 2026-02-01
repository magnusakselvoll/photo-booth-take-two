using PhotoBooth.Application.DTOs;

namespace PhotoBooth.Application.Services;

public interface IPhotoCaptureService
{
    Task<CaptureResultDto> CaptureAsync(CancellationToken cancellationToken = default);
    Task<PhotoDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PhotoDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
