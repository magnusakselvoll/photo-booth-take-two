using PhotoBooth.Application.DTOs;

namespace PhotoBooth.Application.Services;

public interface ISlideshowService
{
    Task<SlideshowPhotoDto?> GetNextAsync(CancellationToken cancellationToken = default);
}
