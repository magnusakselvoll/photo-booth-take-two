using PhotoBooth.Application.Services;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Server;

public class ThumbnailWarmupService(
    IPhotoRepository photoRepository,
    IImageResizer imageResizer,
    ILogger<ThumbnailWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var photos = await photoRepository.GetAllAsync(stoppingToken);

            if (photos.Count == 0)
            {
                logger.LogInformation("Thumbnail warmup: no photos to pre-generate");
                return;
            }

            logger.LogInformation("Thumbnail warmup: pre-generating thumbnails for {Count} photos", photos.Count);

            for (var i = 0; i < photos.Count; i++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var photo = photos[i];
                try
                {
                    await imageResizer.PreGenerateAllSizesAsync(photo.Id, stoppingToken);
                    logger.LogInformation("Thumbnail warmup: pre-generated thumbnails for photo {Current}/{Total}", i + 1, photos.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Thumbnail warmup: failed to pre-generate thumbnails for photo {PhotoId}", photo.Id);
                }
            }

            logger.LogInformation("Thumbnail warmup: completed");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Thumbnail warmup: cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Thumbnail warmup: unexpected error");
        }
    }
}
