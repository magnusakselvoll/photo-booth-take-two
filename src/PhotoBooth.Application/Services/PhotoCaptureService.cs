using Microsoft.Extensions.Logging;
using PhotoBooth.Application.DTOs;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Exceptions;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Services;

public class PhotoCaptureService : IPhotoCaptureService
{
    private readonly ICameraProvider _cameraProvider;
    private readonly IPhotoRepository _photoRepository;
    private readonly IPhotoCodeGenerator _codeGenerator;
    private readonly ILogger<PhotoCaptureService> _logger;

    public PhotoCaptureService(
        ICameraProvider cameraProvider,
        IPhotoRepository photoRepository,
        IPhotoCodeGenerator codeGenerator,
        ILogger<PhotoCaptureService> logger)
    {
        _cameraProvider = cameraProvider;
        _photoRepository = photoRepository;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<CaptureResultDto> CaptureAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting photo capture");

        if (!await _cameraProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogWarning("Camera is not available");
            throw new CameraNotAvailableException();
        }

        var imageData = await _cameraProvider.CaptureAsync(cancellationToken);
        _logger.LogDebug("Captured image with {Size} bytes", imageData.Length);

        var code = await _codeGenerator.GenerateUniqueCodeAsync(
            async c => await _photoRepository.GetByCodeAsync(c, cancellationToken) != null,
            cancellationToken);

        var photo = new Photo
        {
            Id = Guid.NewGuid(),
            Code = code,
            CapturedAt = DateTime.UtcNow
        };

        var savedPhoto = await _photoRepository.SaveAsync(photo, imageData, cancellationToken);
        _logger.LogInformation("Photo captured and saved with code {Code} and id {PhotoId}", savedPhoto.Code, savedPhoto.Id);

        return new CaptureResultDto(savedPhoto.Id, savedPhoto.Code, savedPhoto.CapturedAt);
    }

    public async Task<PhotoDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Looking up photo by code {Code}", code);
        var photo = await _photoRepository.GetByCodeAsync(code, cancellationToken);

        if (photo is null)
        {
            _logger.LogDebug("Photo with code {Code} not found", code);
            return null;
        }

        return new PhotoDto(photo.Id, photo.Code, photo.CapturedAt);
    }

    public async Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving image data for photo {PhotoId}", id);
        return await _photoRepository.GetImageDataAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<PhotoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all photos");
        var photos = await _photoRepository.GetAllAsync(cancellationToken);
        return photos.Select(p => new PhotoDto(p.Id, p.Code, p.CapturedAt)).ToList();
    }
}
