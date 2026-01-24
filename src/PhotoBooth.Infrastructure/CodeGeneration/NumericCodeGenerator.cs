using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Infrastructure.CodeGeneration;

public class NumericCodeGenerator : IPhotoCodeGenerator
{
    private readonly int _codeLength;
    private readonly int _maxAttempts;

    public NumericCodeGenerator(int codeLength = 6, int maxAttempts = 100)
    {
        _codeLength = codeLength;
        _maxAttempts = maxAttempts;
    }

    public async Task<string> GenerateUniqueCodeAsync(
        Func<string, Task<bool>> isCodeTaken,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < _maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var code = GenerateCode();
            if (!await isCodeTaken(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException($"Failed to generate unique code after {_maxAttempts} attempts");
    }

    private string GenerateCode()
    {
        var maxValue = (int)Math.Pow(10, _codeLength);
        var value = Random.Shared.Next(maxValue);
        return value.ToString().PadLeft(_codeLength, '0');
    }
}
