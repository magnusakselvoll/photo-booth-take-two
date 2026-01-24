using Microsoft.Extensions.Logging;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Services;

public class SlideshowService : ISlideshowService
{
    private readonly IPhotoRepository _photoRepository;
    private readonly ILogger<SlideshowService> _logger;
    private readonly string _baseUrl;

    public SlideshowService(
        IPhotoRepository photoRepository,
        ILogger<SlideshowService> logger,
        string baseUrl = "/api/photos")
    {
        _photoRepository = photoRepository;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<SlideshowPhotoDto?> GetNextAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting next random photo for slideshow");
        var photo = await _photoRepository.GetRandomAsync(cancellationToken);

        if (photo is null)
        {
            _logger.LogDebug("No photos available for slideshow");
            return null;
        }

        return ToDto(photo);
    }

    public async Task<IReadOnlyList<SlideshowPhotoDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting {Count} recent photos for slideshow", count);
        var photos = await _photoRepository.GetRecentAsync(count, cancellationToken);
        _logger.LogDebug("Retrieved {ActualCount} recent photos", photos.Count);
        return photos.Select(ToDto).ToList();
    }

    private SlideshowPhotoDto ToDto(Photo photo)
    {
        return new SlideshowPhotoDto(
            photo.Id,
            photo.Code,
            photo.CapturedAt,
            $"{_baseUrl}/{photo.Id}/image");
    }
}
