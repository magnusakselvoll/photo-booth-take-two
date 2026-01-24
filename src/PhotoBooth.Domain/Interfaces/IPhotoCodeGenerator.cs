namespace PhotoBooth.Domain.Interfaces;

public interface IPhotoCodeGenerator
{
    Task<string> GenerateUniqueCodeAsync(Func<string, Task<bool>> isCodeTaken, CancellationToken cancellationToken = default);
}
