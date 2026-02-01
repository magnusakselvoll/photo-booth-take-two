using PhotoBooth.Domain.Entities;
using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.Storage;

public class InMemoryPhotoRepository : IPhotoRepository
{
    private readonly List<Photo> _photos = new();
    private readonly Dictionary<Guid, byte[]> _imageData = new();
    private readonly object _lock = new();

    public Task<Photo> SaveAsync(Photo photo, byte[] imageData, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _photos.Add(photo);
            _imageData[photo.Id] = imageData;
        }
        return Task.FromResult(photo);
    }

    public Task<Photo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_photos.FirstOrDefault(p => p.Code == code));
        }
    }

    public Task<Photo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
        }
    }

    public Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_imageData.TryGetValue(id, out var data) ? data : null);
        }
    }

    public Task<Photo?> GetRandomAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_photos.Count == 0)
            {
                return Task.FromResult<Photo?>(null);
            }

            var index = Random.Shared.Next(_photos.Count);
            return Task.FromResult<Photo?>(_photos[index]);
        }
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_photos.Count);
        }
    }

    public Task<IReadOnlyList<Photo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Photo>>(
                _photos.OrderByDescending(p => p.CapturedAt).ToList());
        }
    }
}
