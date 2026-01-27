using System.Text.Json;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Storage;

public class FileSystemPhotoRepository : IPhotoRepository
{
    private readonly string _storagePath;
    private readonly string _metadataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Photo>? _photosCache;

    public FileSystemPhotoRepository(string basePath, string eventName)
    {
        _storagePath = Path.Combine(basePath, SanitizeEventName(eventName));
        _metadataFile = Path.Combine(_storagePath, "photos.json");
        Directory.CreateDirectory(_storagePath);
    }

    private static string SanitizeEventName(string eventName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", eventName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    public async Task<Photo> SaveAsync(Photo photo, byte[] imageData, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var paddedCode = photo.Code.PadLeft(5, '0');
            var fileName = $"{paddedCode}-{photo.Id}.jpg";
            photo.FilePath = Path.Combine(_storagePath, fileName);

            await File.WriteAllBytesAsync(photo.FilePath, imageData, cancellationToken);

            var photos = await LoadPhotosAsync(cancellationToken);
            photos.Add(photo);
            await SavePhotosAsync(photos, cancellationToken);

            _photosCache = photos;
            return photo;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Photo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotosAsync(cancellationToken);
        return photos.FirstOrDefault(p => p.Code == code);
    }

    public async Task<Photo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotosAsync(cancellationToken);
        return photos.FirstOrDefault(p => p.Id == id);
    }

    public async Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var photo = await GetByIdAsync(id, cancellationToken);
        if (photo is null || string.IsNullOrEmpty(photo.FilePath) || !File.Exists(photo.FilePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(photo.FilePath, cancellationToken);
    }

    public async Task<Photo?> GetRandomAsync(CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotosAsync(cancellationToken);
        if (photos.Count == 0)
        {
            return null;
        }

        var index = Random.Shared.Next(photos.Count);
        return photos[index];
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotosAsync(cancellationToken);
        return photos.Count;
    }

    private async Task<List<Photo>> GetPhotosAsync(CancellationToken cancellationToken)
    {
        if (_photosCache is not null)
        {
            return _photosCache;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _photosCache = await LoadPhotosAsync(cancellationToken);
            return _photosCache;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<Photo>> LoadPhotosAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_metadataFile))
        {
            return new List<Photo>();
        }

        var json = await File.ReadAllTextAsync(_metadataFile, cancellationToken);
        return JsonSerializer.Deserialize<List<Photo>>(json) ?? new List<Photo>();
    }

    private async Task SavePhotosAsync(List<Photo> photos, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(photos, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_metadataFile, json, cancellationToken);
    }
}
