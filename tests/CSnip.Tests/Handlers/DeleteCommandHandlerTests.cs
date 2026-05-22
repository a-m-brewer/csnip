using AwesomeAssertions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Services;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Handlers;

[TestFixture]
public class DeleteCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
    }

    private void SetupSelectedSnippet(IReadOnlyList<Snippet> snippets, Snippet? selected)
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<ISnippetPromptService>()
            .Setup(p => p.SelectSnippetAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selected);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.DeleteAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Test]
    public async Task HandleAsync_SelectedSnippet_DeletesSnippetAndReturnsZero()
    {
        var selected = new Snippet("git status", "Check repo", ["git"]);
        IReadOnlyList<Snippet> snippets = [selected];
        SetupSelectedSnippet(snippets, selected);

        var sut = _mocker.CreateInstance<DeleteCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.DeleteAsync(selected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_NoSnippets_DoesNotPromptOrDelete()
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = _mocker.CreateInstance<DeleteCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetPromptService>()
            .Verify(p => p.SelectSnippetAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.DeleteAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_NoSelection_DoesNotDelete()
    {
        var existing = new Snippet("git status", null, []);
        IReadOnlyList<Snippet> snippets = [existing];
        SetupSelectedSnippet(snippets, null);

        var sut = _mocker.CreateInstance<DeleteCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.DeleteAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_DeleteFails_ReturnsOne()
    {
        var selected = new Snippet("git status", null, []);
        IReadOnlyList<Snippet> snippets = [selected];
        SetupSelectedSnippet(snippets, selected);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.DeleteAsync(selected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = _mocker.CreateInstance<DeleteCommandHandler>();
        var result = await sut.HandleAsync(null!, CancellationToken.None);

        result.Should().Be(1);
    }
}
