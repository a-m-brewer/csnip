using System.CommandLine;
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
public class EditCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
    }

    private static ParseResult BuildParseResult(params string[] args)
    {
        var command = new Command("edit");
        var symbols = new EditCommandHandler.Symbols();
        foreach (var arg in symbols.Arguments) command.Add(arg);
        foreach (var opt in symbols.Options) command.Add(opt);
        return command.Parse(args);
    }

    private void SetupLineReader(params string?[] lines)
    {
        var seq = _mocker.GetMock<ILineReader>().SetupSequence(r => r.ReadLine());
        foreach (var line in lines) seq.Returns(line);
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
            .Setup(r => r.UpdateAsync(It.IsAny<Snippet>(), It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Test]
    public async Task HandleAsync_WithCommandDescriptionAndTags_UpdatesSelectedSnippetAndReturnsZero()
    {
        var existing = new Snippet("git status", "Old description", ["git"]);
        IReadOnlyList<Snippet> snippets = [existing];
        Snippet? updated = null;
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, Snippet, CancellationToken>((_, s, _) => updated = s)
            .ReturnsAsync(true);

        var parseResult = BuildParseResult("git log --oneline", "--description", "Show log", "--tags", "git", "--tags", "history");
        var sut = _mocker.CreateInstance<EditCommandHandler>();

        var result = await sut.HandleAsync(parseResult, CancellationToken.None);

        result.Should().Be(0);
        updated.Should().NotBeNull();
        updated!.Command.Should().Be("git log --oneline");
        updated.Description.Should().Be("Show log");
        updated.Tags.Should().BeEquivalentTo(new[] { "git", "history" });
        _mocker.GetMock<ILineReader>().Verify(r => r.ReadLine(), Times.Never);
    }

    [Test]
    public async Task HandleAsync_WithCommandOnly_KeepsExistingDescriptionAndTags()
    {
        var existing = new Snippet("git status", "Check repo", ["git", "vcs"]);
        IReadOnlyList<Snippet> snippets = [existing];
        Snippet? updated = null;
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, Snippet, CancellationToken>((_, s, _) => updated = s)
            .ReturnsAsync(true);

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("git status --short"), CancellationToken.None);

        result.Should().Be(0);
        updated.Should().NotBeNull();
        updated!.Command.Should().Be("git status --short");
        updated.Description.Should().Be("Check repo");
        updated.Tags.Should().BeEquivalentTo(new[] { "git", "vcs" });
    }

    [Test]
    public async Task HandleAsync_WithEmptyDescriptionAndTagsOption_ClearsOptionalFields()
    {
        var existing = new Snippet("git status", "Check repo", ["git"]);
        IReadOnlyList<Snippet> snippets = [existing];
        Snippet? updated = null;
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, Snippet, CancellationToken>((_, s, _) => updated = s)
            .ReturnsAsync(true);

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("--description", "", "--tags"), CancellationToken.None);

        result.Should().Be(0);
        updated.Should().NotBeNull();
        updated!.Command.Should().Be("git status");
        updated.Description.Should().BeNull();
        updated.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task HandleAsync_NoArgs_PromptsForAllFieldsAndUpdatesSnippet()
    {
        var existing = new Snippet("git status", "Check repo", ["git"]);
        IReadOnlyList<Snippet> snippets = [existing];
        Snippet? updated = null;
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, Snippet, CancellationToken>((_, s, _) => updated = s)
            .ReturnsAsync(true);
        SetupLineReader("docker ps", "List containers", "docker, containers");

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        updated.Should().NotBeNull();
        updated!.Command.Should().Be("docker ps");
        updated.Description.Should().Be("List containers");
        updated.Tags.Should().BeEquivalentTo(new[] { "docker", "containers" });
    }

    [Test]
    public async Task HandleAsync_NoArgs_BlankInputsKeepExistingFields()
    {
        var existing = new Snippet("git status", "Check repo", ["git"]);
        IReadOnlyList<Snippet> snippets = [existing];
        Snippet? updated = null;
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, Snippet, CancellationToken>((_, s, _) => updated = s)
            .ReturnsAsync(true);
        SetupLineReader("", "", "");

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        updated.Should().BeEquivalentTo(existing);
    }

    [Test]
    public async Task HandleAsync_NoSnippets_DoesNotPromptOrUpdate()
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("git status"), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetPromptService>()
            .Verify(p => p.SelectSnippetAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.UpdateAsync(It.IsAny<Snippet>(), It.IsAny<Snippet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_NoSelection_DoesNotUpdate()
    {
        var existing = new Snippet("git status", null, []);
        IReadOnlyList<Snippet> snippets = [existing];
        SetupSelectedSnippet(snippets, null);

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("git log"), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.UpdateAsync(It.IsAny<Snippet>(), It.IsAny<Snippet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_UpdateFails_ReturnsOne()
    {
        var existing = new Snippet("git status", null, []);
        IReadOnlyList<Snippet> snippets = [existing];
        SetupSelectedSnippet(snippets, existing);
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.UpdateAsync(existing, It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = _mocker.CreateInstance<EditCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("git log"), CancellationToken.None);

        result.Should().Be(1);
    }
}
