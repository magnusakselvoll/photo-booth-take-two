using PhotoBooth.Domain.Entities;
using PhotoBooth.Infrastructure.Storage;

namespace PhotoBooth.Infrastructure.Tests;

[TestClass]
public sealed class FileSystemPhotoRepositoryTests
{
    private string _testBasePath = null!;

    [TestInitialize]
    public void Setup()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), "PhotoBoothTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testBasePath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testBasePath))
        {
            Directory.Delete(_testBasePath, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsync_CreatesCorrectlyNamedFile()
    {
        // Arrange
        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");
        var photoId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        var photo = new Photo
        {
            Id = photoId,
            Code = "42",
            CapturedAt = DateTime.UtcNow
        };
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF }; // Minimal JPEG-like header

        // Act
        var saved = await repository.SaveAsync(photo, imageData);

        // Assert
        Assert.IsNotNull(saved.FilePath);
        Assert.IsTrue(File.Exists(saved.FilePath));
        var fileName = Path.GetFileName(saved.FilePath);
        Assert.AreEqual("00042-550e8400-e29b-41d4-a716-446655440000.jpg", fileName);
    }

    [TestMethod]
    public async Task GetAllAsync_ParsesExistingFiles()
    {
        // Arrange
        var eventPath = Path.Combine(_testBasePath, "TestEvent");
        Directory.CreateDirectory(eventPath);

        // Create test files with correct naming pattern
        var file1 = Path.Combine(eventPath, "00001-11111111-1111-1111-1111-111111111111.jpg");
        var file2 = Path.Combine(eventPath, "00002-22222222-2222-2222-2222-222222222222.jpg");
        await File.WriteAllBytesAsync(file1, new byte[] { 0xFF });
        await File.WriteAllBytesAsync(file2, new byte[] { 0xFF });

        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var photos = await repository.GetAllAsync();

        // Assert
        Assert.HasCount(2, photos);
        Assert.IsTrue(photos.Any(p => p.Code == "1" && p.Id == Guid.Parse("11111111-1111-1111-1111-111111111111")));
        Assert.IsTrue(photos.Any(p => p.Code == "2" && p.Id == Guid.Parse("22222222-2222-2222-2222-222222222222")));
    }

    [TestMethod]
    public async Task GetAllAsync_SkipsMalformedFilenames()
    {
        // Arrange
        var eventPath = Path.Combine(_testBasePath, "TestEvent");
        Directory.CreateDirectory(eventPath);

        // Create files with various malformed names
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "invalid.jpg"), new byte[] { 0xFF });
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "no-guid-here.jpg"), new byte[] { 0xFF });
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "-11111111-1111-1111-1111-111111111111.jpg"), new byte[] { 0xFF }); // No code
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "photos.json"), new byte[] { 0xFF }); // Not a .jpg

        // Create one valid file
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00001-11111111-1111-1111-1111-111111111111.jpg"), new byte[] { 0xFF });

        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var photos = await repository.GetAllAsync();

        // Assert
        Assert.HasCount(1, photos);
        Assert.AreEqual("1", photos[0].Code);
    }

    [TestMethod]
    public async Task GetByCodeAsync_FindsPhotoByCode()
    {
        // Arrange
        var eventPath = Path.Combine(_testBasePath, "TestEvent");
        Directory.CreateDirectory(eventPath);

        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00001-11111111-1111-1111-1111-111111111111.jpg"), new byte[] { 0xFF });
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00042-22222222-2222-2222-2222-222222222222.jpg"), new byte[] { 0xFF });

        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var photo = await repository.GetByCodeAsync("42");

        // Assert
        Assert.IsNotNull(photo);
        Assert.AreEqual("42", photo.Code);
        Assert.AreEqual(Guid.Parse("22222222-2222-2222-2222-222222222222"), photo.Id);
    }

    [TestMethod]
    public async Task GetByCodeAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var photo = await repository.GetByCodeAsync("999");

        // Assert
        Assert.IsNull(photo);
    }

    [TestMethod]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var eventPath = Path.Combine(_testBasePath, "TestEvent");
        Directory.CreateDirectory(eventPath);

        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00001-11111111-1111-1111-1111-111111111111.jpg"), new byte[] { 0xFF });
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00002-22222222-2222-2222-2222-222222222222.jpg"), new byte[] { 0xFF });
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00003-33333333-3333-3333-3333-333333333333.jpg"), new byte[] { 0xFF });

        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var count = await repository.GetCountAsync();

        // Assert
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task GetAllAsync_ParsesCodeZeroCorrectly()
    {
        // Arrange
        var eventPath = Path.Combine(_testBasePath, "TestEvent");
        Directory.CreateDirectory(eventPath);

        // "00000" should parse to "0"
        await File.WriteAllBytesAsync(Path.Combine(eventPath, "00000-11111111-1111-1111-1111-111111111111.jpg"), new byte[] { 0xFF });

        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");

        // Act
        var photos = await repository.GetAllAsync();

        // Assert
        Assert.HasCount(1, photos);
        Assert.AreEqual("0", photos[0].Code);
    }

    [TestMethod]
    public async Task SaveAsync_AddsPhotoToCache()
    {
        // Arrange
        var repository = new FileSystemPhotoRepository(_testBasePath, "TestEvent");
        var photo1 = new Photo
        {
            Id = Guid.NewGuid(),
            Code = "1",
            CapturedAt = DateTime.UtcNow
        };
        var photo2 = new Photo
        {
            Id = Guid.NewGuid(),
            Code = "2",
            CapturedAt = DateTime.UtcNow
        };

        // Act - save first photo, then get all to populate cache, then save second
        await repository.SaveAsync(photo1, new byte[] { 0xFF });
        var countAfterFirst = await repository.GetCountAsync();
        await repository.SaveAsync(photo2, new byte[] { 0xFF });
        var countAfterSecond = await repository.GetCountAsync();

        // Assert
        Assert.AreEqual(1, countAfterFirst);
        Assert.AreEqual(2, countAfterSecond);
    }
}
