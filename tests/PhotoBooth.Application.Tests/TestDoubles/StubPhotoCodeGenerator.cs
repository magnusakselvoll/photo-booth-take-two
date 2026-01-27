using PhotoBooth.Domain.Interfaces;

namespace PhotoBooth.Application.Tests.TestDoubles;

public sealed class StubPhotoCodeGenerator : IPhotoCodeGenerator
{
    public string CodeToReturn { get; set; } = "123456";

    public Task<string> GenerateUniqueCodeAsync(Func<string, Task<bool>> isCodeTaken, CancellationToken cancellationToken = default)
        => Task.FromResult(CodeToReturn);
}
