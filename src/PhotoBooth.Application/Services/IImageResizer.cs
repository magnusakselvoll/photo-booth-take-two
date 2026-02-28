namespace PhotoBooth.Application.Services;

public interface IImageResizer
{
    static readonly int[] AllowedWidths = [200, 400, 800, 1200, 1920];

    static int SnapToAllowedWidth(int requested)
    {
        foreach (var w in AllowedWidths)
        {
            if (w >= requested)
                return w;
        }
        return AllowedWidths[^1];
    }

    Task<byte[]?> GetResizedImageAsync(Guid photoId, int width, CancellationToken ct);

    async Task PreGenerateAllSizesAsync(Guid photoId, CancellationToken ct)
    {
        foreach (var width in AllowedWidths)
        {
            await GetResizedImageAsync(photoId, width, ct);
        }
    }
}
