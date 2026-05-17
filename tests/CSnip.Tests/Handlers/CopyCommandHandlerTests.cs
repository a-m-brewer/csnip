using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Services;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Handlers;

[TestFixture]
public class CopyCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
    }

    [Test]
    public async Task HandleAsync_NoSnippets_DoesNotCopyAndReturnsZero()
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_SnippetSelected_CopiesCommandAndReturnsZero()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync("git status", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_OrchestratorReturnsNull_DoesNotCopyAndReturnsZero()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_WithSnippets_PassesSnippetsToOrchestrator()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("cmd", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        await sut.HandleAsync(null!, CancellationToken.None);

        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveSnippetCommandAsync(snippets, It.IsAny<CancellationToken>()), Times.Once);
    }
}
