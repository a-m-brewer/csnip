using System.CommandLine;
using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Persistence;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Handlers;

[TestFixture]
public class AddCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
    }

    private static ParseResult BuildParseResult(params string[] args)
    {
        var command = new Command("add");
        var symbols = new AddCommandHandler.Symbols();
        foreach (var arg in symbols.Arguments) command.Add(arg);
        foreach (var opt in symbols.Options) command.Add(opt);
        return command.Parse(args);
    }

    private void SetupLineReader(params string?[] lines)
    {
        var seq = _mocker.GetMock<ILineReader>().SetupSequence(r => r.ReadLine());
        foreach (var line in lines) seq.Returns(line);
    }

    [Test]
    public async Task HandleAsync_WithCommandDescriptionAndTags_SavesSnippetAndReturnsZero()
    {
        Snippet? saved = null;
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.AddAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .Callback<Snippet, CancellationToken>((s, _) => saved = s);

        var parseResult = BuildParseResult("git status", "--description", "Check repo", "--tags", "git", "--tags", "vcs");
        var sut = _mocker.CreateInstance<AddCommandHandler>();

        var result = await sut.HandleAsync(parseResult, CancellationToken.None);

        result.Should().Be(0);
        saved.Should().NotBeNull();
        saved!.Command.Should().Be("git status");
        saved.Description.Should().Be("Check repo");
        saved.Tags.Should().BeEquivalentTo(new[] { "git", "vcs" });
    }

    [Test]
    public async Task HandleAsync_WithCommandOnly_SavesSnippetWithNullDescriptionAndEmptyTags()
    {
        var parseResult = BuildParseResult("git status");
        var sut = _mocker.CreateInstance<AddCommandHandler>();

        var result = await sut.HandleAsync(parseResult, CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.AddAsync(
                It.Is<Snippet>(s =>
                    s.Command == "git status" &&
                    s.Description == null &&
                    s.Tags.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_NoArgs_PromptsForAllFieldsAndSavesSnippet()
    {
        SetupLineReader("git status", "Check repo status", "git, vcs");

        var sut = _mocker.CreateInstance<AddCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.AddAsync(
                It.Is<Snippet>(s =>
                    s.Command == "git status" &&
                    s.Description == "Check repo status" &&
                    s.Tags.Length == 2 && s.Tags[0] == "git" && s.Tags[1] == "vcs"),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_NoArgs_EmptyOptionalFields_SavesWithNullDescriptionAndEmptyTags()
    {
        SetupLineReader("git status", "", "");

        var sut = _mocker.CreateInstance<AddCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.AddAsync(
                It.Is<Snippet>(s =>
                    s.Command == "git status" &&
                    s.Description == null &&
                    s.Tags.Length == 0),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_NoArgs_SingleTag_SavesSnippetWithSingleTag()
    {
        SetupLineReader("docker ps", "List running containers", "docker");

        var sut = _mocker.CreateInstance<AddCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<ISnippetRepository>()
            .Verify(r => r.AddAsync(
                It.Is<Snippet>(s => s.Tags.Length == 1 && s.Tags[0] == "docker"),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}
