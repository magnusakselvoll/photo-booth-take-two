using Microsoft.Extensions.Logging;
using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Storage;

public class FileSystemPhotoRepository : IPhotoRepository
{
    private readonly string _storagePath;
    private readonly ILogger<FileSystemPhotoRepository>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Photo>? _photosCache;

    public FileSystemPhotoRepository(string basePath, string eventName, ILogger<FileSystemPhotoRepository>? logger = null)
    {
        _storagePath = Path.Combine(basePath, SanitizeEventName(eventName));
        _logger = logger;
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

            // Add to cache if it exists
            _photosCache?.Add(photo);

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

    public async Task<IReadOnlyList<Photo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var photos = await GetPhotosAsync(cancellationToken);
        return photos.OrderBy(p => int.TryParse(p.Code, out var code) ? code : int.MaxValue).ToList();
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

    private Task<List<Photo>> LoadPhotosAsync(CancellationToken cancellationToken)
    {
        var photos = new List<Photo>();

        if (!Directory.Exists(_storagePath))
        {
            return Task.FromResult(photos);
        }

        foreach (var filePath in Directory.EnumerateFiles(_storagePath, "*.jpg"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var photo = TryParsePhotoFromFile(filePath);
            if (photo is not null)
            {
                photos.Add(photo);
            }
            else
            {
                _logger?.LogWarning("Skipping malformed photo filename: {FileName}", Path.GetFileName(filePath));
            }
        }

        return Task.FromResult(photos);
    }

    private static Photo? TryParsePhotoFromFile(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Expected format: {paddedCode}-{guid}
        // Example: 00001-550e8400-e29b-41d4-a716-446655440000
        var dashIndex = fileName.IndexOf('-');
        if (dashIndex < 1)
        {
            return null;
        }

        var codeStr = fileName[..dashIndex].TrimStart('0');
        if (string.IsNullOrEmpty(codeStr))
        {
            codeStr = "0";
        }

        var guidStr = fileName[(dashIndex + 1)..];
        if (!Guid.TryParse(guidStr, out var id))
        {
            return null;
        }

        return new Photo
        {
            Id = id,
            Code = codeStr,
            FilePath = filePath,
            CapturedAt = File.GetCreationTimeUtc(filePath)
        };
    }
}
