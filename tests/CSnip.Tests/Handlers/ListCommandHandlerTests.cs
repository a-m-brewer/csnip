using System.CommandLine;
using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Handlers;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Pipeline;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Handlers;

[TestFixture]
public class ListCommandHandlerTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp() => _mocker = new AutoMocker();

    private static ParseResult BuildParseResult(params string[] args)
    {
        var command = new Command("list");
        var symbols = new ListCommandHandler.Symbols();
        foreach (var opt in symbols.Options) command.Add(opt);
        return command.Parse(args);
    }

    [Test]
    public async Task HandleAsync_TextFormat_CallsWriterWithTextFormat()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsOutputRedirected).Returns(false);
        _mocker.Use(new FormatResolver());

        var sut = _mocker.CreateInstance<ListCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IPipelineWriter>()
            .Verify(w => w.WriteAsync(snippets, OutputFormat.Text, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_JsonFormat_CallsWriterWithJsonFormat()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsOutputRedirected).Returns(false);
        _mocker.Use(new FormatResolver());

        var sut = _mocker.CreateInstance<ListCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult("--format", "json"), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IPipelineWriter>()
            .Verify(w => w.WriteAsync(snippets, OutputFormat.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_AutoFormat_RedirectedOutput_CallsWriterWithJsonFormat()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", null, [])];
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippets);
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsOutputRedirected).Returns(true);
        _mocker.Use(new FormatResolver());

        var sut = _mocker.CreateInstance<ListCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IPipelineWriter>()
            .Verify(w => w.WriteAsync(snippets, OutputFormat.Json, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_EmptyRepo_CallsWriterWithEmptyList()
    {
        _mocker.GetMock<ISnippetRepository>()
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.IsOutputRedirected).Returns(false);
        _mocker.Use(new FormatResolver());

        var sut = _mocker.CreateInstance<ListCommandHandler>();
        var result = await sut.HandleAsync(BuildParseResult(), CancellationToken.None);

        result.Should().Be(0);
        _mocker.GetMock<IPipelineWriter>()
            .Verify(w => w.WriteAsync(
                It.Is<IReadOnlyList<Snippet>>(l => l.Count == 0),
                It.IsAny<OutputFormat>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}
