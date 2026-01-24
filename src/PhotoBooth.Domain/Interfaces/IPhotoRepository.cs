using PhotoBooth.Domain.Entities;

namespace PhotoBooth.Domain.Interfaces;

public interface IPhotoRepository
{
    Task<Photo> SaveAsync(Photo photo, byte[] imageData, CancellationToken cancellationToken = default);
    Task<Photo?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Photo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]?> GetImageDataAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Photo>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<Photo?> GetRandomAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
