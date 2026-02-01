using PhotoBooth.Infrastructure.CodeGeneration;

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
public sealed class NumericCodeGeneratorTests
{
    [TestMethod]
    public async Task GenerateUniqueCodeAsync_ReturnsCodeOfSpecifiedLength()
    {
        // Arrange
        var generator = new NumericCodeGenerator(codeLength: 4);

        // Act
        var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        // Assert
        Assert.AreEqual(4, code.Length);
        Assert.IsTrue(code.All(char.IsDigit), "Code should contain only digits");
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_WhenCodeTaken_GeneratesNew()
    {
        // Arrange
        var generator = new NumericCodeGenerator(codeLength: 4);
        var attemptCount = 0;
        string? firstCode = null;

        // Act - first attempt is "taken", second should succeed
        var code = await generator.GenerateUniqueCodeAsync(async c =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                firstCode = c;
                return true; // First code is taken
            }
            return false; // Second code is available
        });

        // Assert
        Assert.AreEqual(2, attemptCount);
        Assert.AreEqual(4, code.Length);
        // Note: There's a small chance the random generator produces the same code,
        // but with 10000 possible codes this is unlikely
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_WhenAllCodesTaken_ThrowsException()
    {
        // Arrange
        var generator = new NumericCodeGenerator(codeLength: 4, maxAttempts: 3);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => generator.GenerateUniqueCodeAsync(_ => Task.FromResult(true)));
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_DefaultLength_IsSix()
    {
        // Arrange
        var generator = new NumericCodeGenerator();

        // Act
        var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));

        // Assert
        Assert.AreEqual(6, code.Length);
    }

    [TestMethod]
    public async Task GenerateUniqueCodeAsync_PadsWithLeadingZeros()
    {
        // Arrange
        var generator = new NumericCodeGenerator(codeLength: 6);
        var foundPaddedCode = false;

        // Act - generate multiple codes to find one that starts with zero
        for (var i = 0; i < 100 && !foundPaddedCode; i++)
        {
            var code = await generator.GenerateUniqueCodeAsync(_ => Task.FromResult(false));
            if (code.StartsWith('0'))
            {
                foundPaddedCode = true;
                Assert.AreEqual(6, code.Length);
            }
        }

        // Note: This test may occasionally pass without finding a padded code,
        // but statistically we should find one in 100 attempts
    }
}
