using PhotoBooth.Application.DTOs;

namespace PhotoBooth.Application.Services;

public interface ISlideshowService
{
    Task<SlideshowPhotoDto?> GetNextAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlideshowPhotoDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}
