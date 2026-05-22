using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CSnip.Models;
using CSnip.Models.Settings;
using CSnip.Persistence;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Persistence;

[TestFixture]
public class SnippetRepositoryTests
{
    private AutoMocker _mocker = null!;
    private const string StorePath = "/fake/csnip/snippets.json";

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
        _mocker.GetMock<IOptions<StoreSettings>>()
            .Setup(o => o.Value)
            .Returns(new StoreSettings { SnippetsPath = StorePath });
    }

    [Test]
    public async Task AddAsync_NoExistingFile_WritesSnippetToNewFile()
    {
        // Arrange
        var writeStream = new NonDisposableMemoryStream();
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(false);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.Create(StorePath))
            .Returns(writeStream);

        var snippet = new Snippet("git status", "Check repo status", ["git"]);
        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        await sut.AddAsync(snippet, CancellationToken.None);

        // Assert
        writeStream.Position = 0;
        var saved = await JsonSerializer.DeserializeAsync(writeStream, SnippetJsonContext.Default.ListSnippet);
        saved.Should().HaveCount(1);
        saved![0].Should().BeEquivalentTo(snippet);
    }

    [Test]
    public async Task AddAsync_ExistingFile_AppendsToExistingSnippets()
    {
        // Arrange
        var existing = new Snippet("ls -la", null, []);
        var readStream = SnippetsToStream([existing]);
        var writeStream = new NonDisposableMemoryStream();

        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(readStream);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.Create(StorePath))
            .Returns(writeStream);

        var newSnippet = new Snippet("git log --oneline", null, ["git"]);
        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        await sut.AddAsync(newSnippet, CancellationToken.None);

        // Assert
        writeStream.Position = 0;
        var saved = await JsonSerializer.DeserializeAsync(writeStream, SnippetJsonContext.Default.ListSnippet);
        saved.Should().HaveCount(2);
        saved.Should().ContainEquivalentOf(existing);
        saved.Should().ContainEquivalentOf(newSnippet);
    }

    [Test]
    public async Task AddAsync_Always_EnsuresDirectoryExists()
    {
        // Arrange
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(false);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.Create(StorePath))
            .Returns(new NonDisposableMemoryStream());

        var sut = _mocker.CreateInstance<SnippetRepository>();
        var snippet = new Snippet("docker ps", null, []);

        // Act
        await sut.AddAsync(snippet, CancellationToken.None);

        // Assert
        _mocker.GetMock<IFileSystem>()
            .Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task UpdateAsync_ExistingSnippet_ReplacesMatchingSnippet()
    {
        // Arrange
        var existing = new Snippet("git status", "Check repo status", ["git"]);
        var other = new Snippet("docker ps", null, ["docker"]);
        var readStream = SnippetsToStream([existing, other]);
        var writeStream = new NonDisposableMemoryStream();

        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(readStream);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.Create(StorePath))
            .Returns(writeStream);

        var equivalentExisting = new Snippet("git status", "Check repo status", ["git"]);
        var updated = new Snippet("git status --short", "Short repo status", ["git", "vcs"]);
        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        var result = await sut.UpdateAsync(equivalentExisting, updated, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        writeStream.Position = 0;
        var saved = await JsonSerializer.DeserializeAsync(writeStream, SnippetJsonContext.Default.ListSnippet);
        saved.Should().BeEquivalentTo(new[] { updated, other }, options => options.WithStrictOrdering());
    }

    [Test]
    public async Task UpdateAsync_MissingSnippet_ReturnsFalseWithoutWriting()
    {
        // Arrange
        var existing = new Snippet("git status", null, ["git"]);
        var readStream = SnippetsToStream([existing]);

        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(readStream);

        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        var result = await sut.UpdateAsync(
            new Snippet("docker ps", null, ["docker"]),
            new Snippet("docker ps -a", null, ["docker"]),
            CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _mocker.GetMock<IFileSystem>()
            .Verify(f => f.Create(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task DeleteAsync_ExistingSnippet_RemovesMatchingSnippet()
    {
        // Arrange
        var target = new Snippet("git status", "Check repo status", ["git"]);
        var other = new Snippet("docker ps", null, ["docker"]);
        var readStream = SnippetsToStream([target, other]);
        var writeStream = new NonDisposableMemoryStream();

        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(readStream);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.Create(StorePath))
            .Returns(writeStream);

        var equivalentTarget = new Snippet("git status", "Check repo status", ["git"]);
        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        var result = await sut.DeleteAsync(equivalentTarget, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        writeStream.Position = 0;
        var saved = await JsonSerializer.DeserializeAsync(writeStream, SnippetJsonContext.Default.ListSnippet);
        saved.Should().BeEquivalentTo(new[] { other }, options => options.WithStrictOrdering());
    }

    [Test]
    public async Task DeleteAsync_MissingSnippet_ReturnsFalseWithoutWriting()
    {
        // Arrange
        var existing = new Snippet("git status", null, ["git"]);
        var readStream = SnippetsToStream([existing]);

        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(readStream);

        var sut = _mocker.CreateInstance<SnippetRepository>();

        // Act
        var result = await sut.DeleteAsync(new Snippet("docker ps", null, ["docker"]), CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _mocker.GetMock<IFileSystem>()
            .Verify(f => f.Create(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GetAllAsync_NoExistingFile_ReturnsEmptyList()
    {
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(false);

        var sut = _mocker.CreateInstance<SnippetRepository>();
        var result = await sut.GetAllAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllAsync_ExistingFile_ReturnsAllSnippets()
    {
        var snippets = new List<Snippet>
        {
            new("git status", "Check status", ["git"]),
            new("docker ps", null, ["docker"])
        };
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.FileExists(StorePath))
            .Returns(true);
        _mocker.GetMock<IFileSystem>()
            .Setup(f => f.OpenRead(StorePath))
            .Returns(SnippetsToStream(snippets));

        var sut = _mocker.CreateInstance<SnippetRepository>();
        var result = await sut.GetAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().ContainEquivalentOf(snippets[0]);
        result.Should().ContainEquivalentOf(snippets[1]);
    }

    private static MemoryStream SnippetsToStream(List<Snippet> snippets)
    {
        var json = JsonSerializer.Serialize(snippets, SnippetJsonContext.Default.ListSnippet);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    // MemoryStream disposes itself when used in `await using` — suppress it so tests can read the result.
    private sealed class NonDisposableMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
