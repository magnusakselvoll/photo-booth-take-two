using PhotoBooth.Application.Services;

namespace PhotoBooth.Application.Tests;

[TestClass]
public sealed class UrlPrefixGeneratorTests
{
    [TestMethod]
    public void Generate_ReturnsSamePrefixForSameInputs()
    {
        var first = UrlPrefixGenerator.Generate("MyEvent", "mysalt");
        var second = UrlPrefixGenerator.Generate("MyEvent", "mysalt");
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Generate_ReturnsDifferentPrefixForDifferentEventNames()
    {
        var prefix1 = UrlPrefixGenerator.Generate("EventA", "mysalt");
        var prefix2 = UrlPrefixGenerator.Generate("EventB", "mysalt");
        Assert.AreNotEqual(prefix1, prefix2);
    }

    [TestMethod]
    public void Generate_ReturnsDifferentPrefixForDifferentSalts()
    {
        var prefix1 = UrlPrefixGenerator.Generate("MyEvent", "salt1");
        var prefix2 = UrlPrefixGenerator.Generate("MyEvent", "salt2");
        Assert.AreNotEqual(prefix1, prefix2);
    }

    [TestMethod]
    public void Generate_ReturnsRequestedLength()
    {
        var prefix = UrlPrefixGenerator.Generate("MyEvent", "mysalt");
        Assert.AreEqual(10, prefix.Length);
    }

    [TestMethod]
    public void Generate_ReturnsOnlyLowercaseAlphanumericCharacters()
    {
        var prefix = UrlPrefixGenerator.Generate("MyEvent-2026", "some salt value");
        Assert.IsTrue(prefix.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')),
            $"Prefix '{prefix}' contains non-alphanumeric characters");
    }

    [TestMethod]
    public void Generate_HandlesEmptyEventName()
    {
        var prefix = UrlPrefixGenerator.Generate("", "mysalt");
        Assert.AreEqual(10, prefix.Length);
    }

    [TestMethod]
    public void Generate_HandlesEmptySalt()
    {
        var prefix = UrlPrefixGenerator.Generate("MyEvent", "");
        Assert.AreEqual(10, prefix.Length);
    }

    [TestMethod]
    public void Generate_HandlesBothEmpty()
    {
        var prefix = UrlPrefixGenerator.Generate("", "");
        Assert.AreEqual(10, prefix.Length);
        Assert.IsTrue(prefix.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')));
    }

    [TestMethod]
    public void Generate_ReturnsCustomLength()
    {
        var prefix = UrlPrefixGenerator.Generate("MyEvent", "mysalt", length: 6);
        Assert.AreEqual(6, prefix.Length);
    }
}
