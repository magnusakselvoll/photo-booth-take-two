using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubPhotoRepository : IPhotoRepository
{
    private readonly Dictionary<Guid, (Photo Photo, byte[] ImageData)> _photos = new();

    public Photo? PhotoToReturnByCode { get; set; }
    public Photo? PhotoToReturnRandom { get; set; }
    public byte[]? ImageDataToReturn { get; set; }

    public List<Photo> SavedPhotos { get; } = [];

    public Task<Photo> SaveAsync(Photo photo, byte[] imageData, CancellationToken cancellationToken = default)
    {
        _photos[photo.Id] = (photo, imageData);
        SavedPhotos.Add(photo);
        return Task.FromResult(photo);
    }

    public Task<Photo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Task.FromResult(PhotoToReturnByCode);

    public Task<Photo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_photos.TryGetValue(id, out var entry) ? entry.Photo : null);

    public Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(ImageDataToReturn);

    public Task<Photo?> GetRandomAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(PhotoToReturnRandom);

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_photos.Count);

    public Task<IReadOnlyList<Photo>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Photo>>(_photos.Values.Select(p => p.Photo).OrderBy(p => int.TryParse(p.Code, out var code) ? code : int.MaxValue).ToList());

    public Task<IReadOnlyList<Photo>> GetPageAsync(int limit, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var cursorValue = cursor is not null && int.TryParse(cursor, out var c) ? c : int.MaxValue;
        var page = _photos.Values
            .Select(p => p.Photo)
            .Where(p => int.TryParse(p.Code, out var code) && code < cursorValue)
            .OrderByDescending(p => int.Parse(p.Code))
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<Photo>>(page);
    }
}
