using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.CodeGeneration;

public class SequentialCodeGenerator : IPhotoCodeGenerator
{
    private readonly Func<CancellationToken, Task<int>> _getCountAsync;

    public SequentialCodeGenerator(Func<CancellationToken, Task<int>> getCountAsync)
    {
        _getCountAsync = getCountAsync;
    }

    public async Task<string> GenerateUniqueCodeAsync(
        Func<string, Task<bool>> isCodeTaken,
        CancellationToken cancellationToken = default)
    {
        var currentCount = await _getCountAsync(cancellationToken);
        return (currentCount + 1).ToString();
    }
}
