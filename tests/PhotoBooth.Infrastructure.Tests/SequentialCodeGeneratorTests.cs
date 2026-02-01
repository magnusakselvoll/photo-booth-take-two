using PhotoBooth.Infrastructure.CodeGeneration;

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
public sealed class SequentialCodeGeneratorTests
{
    [TestMethod]
    public async Task GenerateUniqueCodeAsync_ReturnsIncrementedCode()
    {
        // Arrange
        var generator = new SequentialCodeGenerator(_ => Task.FromResult(5));

        // Act
        var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        // Assert
        Assert.AreEqual("6", code);
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_WithZeroCount_ReturnsOne()
    {
        // Arrange
        var generator = new SequentialCodeGenerator(_ => Task.FromResult(0));

        // Act
        var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        // Assert
        Assert.AreEqual("1", code);
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_IgnoresIsCodeTakenCallback()
    {
        // Arrange - isCodeTaken always returns true, but sequential generator ignores it
        var generator = new SequentialCodeGenerator(_ => Task.FromResult(10));

        // Act
        var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(true));

        // Assert - should still return the next sequential code
        Assert.AreEqual("11", code);
    }
}
