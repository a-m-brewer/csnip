using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Pipeline;
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
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsInputRedirected).Returns(false);
    }

    // ── non-redirected (existing behaviour) ──────────────────────────────────

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

    // ── piped input ───────────────────────────────────────────────────────────

    private void SetupPipedInput(IReadOnlyList<Snippet> candidates)
    {
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsInputRedirected).Returns(true);
        _mocker.GetMock<IPipelineReader>()
            .Setup(r => r.ReadAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
    }

    [Test]
    public async Task HandleAsync_PipedEmpty_DoesNotCopyAndReturnsZero()
    {
        SetupPipedInput([]);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_PipedSingle_SkipsPickerAndCopiesDirectly()
    {
        var snippet = new Snippet("git status", null, []);
        SetupPipedInput([snippet]);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveCommandAsync(snippet, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync("git status", It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_PipedSingle_OrchestratorReturnsNull_DoesNotCopy()
    {
        var snippet = new Snippet("git status", null, []);
        SetupPipedInput([snippet]);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveCommandAsync(snippet, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IClipboardService>()
            .Verify(c => c.SetTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_PipedMany_ShowsPickerOverNarrowedList()
    {
        IReadOnlyList<Snippet> candidates =
        [
            new Snippet("git status", null, []),
            new Snippet("git log", null, []),
        ];
        SetupPipedInput(candidates);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(candidates, It.IsAny<CancellationToken>()))
            .ReturnsAsync("git status");

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveSnippetCommandAsync(candidates, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Verify(o => o.ResolveCommandAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_NonRedirected_DoesNotReadFromPipelineReader()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetSelectionOrchestrator>()
            .Setup(o => o.ResolveSnippetCommandAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = _mocker.CreateInstance<CopyCommandHandler>();
        await sut.HandleAsync(null!, CancellationToken.None);

        _mocker.GetMock<IPipelineReader>()
            .Verify(r => r.ReadAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
