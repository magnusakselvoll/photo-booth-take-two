using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.CodeGeneration;

public class SequentialCodeGenerator : IPhotoCodeGenerator
{
    private readonly Func<CancellationToken, Task<int>> _getCountAsync;

    public SequentialCodeGenerator(Func<CancellationToken, Task<int>> getCountAsync)
    {
        _getCountAsync = getCountAsync;
    }

    /// <summary>
    /// Generates a sequential code based on the current photo count.
    /// </summary>
    /// <param name="isCodeTaken">Not used - sequential codes are inherently unique since each
    /// code is derived from the current count which only increases.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> GenerateUniqueCodeAsync(
        Func<string, Task<bool>> isCodeTaken,
        CancellationToken cancellationToken = default)
    {
        var currentCount = await _getCountAsync(cancellationToken);
        return (currentCount + 1).ToString();
    }
}
