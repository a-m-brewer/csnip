using System.Text;
using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Pipeline;
using Moq;
using Moq.AutoMock;
using Spectre.Console.Testing;

namespace CSnip.Tests.Pipeline;

[TestFixture]
public class PipelineWriterTests
{
    private AutoMocker _mocker = null!;
    private StringBuilder _output = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
        _output = new StringBuilder();
        var writer = new StringWriter(_output);
        _mocker.GetMock<IConsoleEnvironment>().Setup(e => e.Out).Returns(writer);
        _mocker.Use<Spectre.Console.IAnsiConsole>(new TestConsole());
    }

    private PipelineWriter CreateSut() => _mocker.CreateInstance<PipelineWriter>();

    [Test]
    public async Task WriteAsync_JsonFormat_NoSnippets_WritesNothing()
    {
        await CreateSut().WriteAsync([], OutputFormat.Json, CancellationToken.None);
        _output.ToString().Should().BeEmpty();
    }

    [Test]
    public async Task WriteAsync_JsonFormat_SingleSnippet_WritesNdjsonLine()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", "Check repo", ["git"])];
        await CreateSut().WriteAsync(snippets, OutputFormat.Json, CancellationToken.None);

        var lines = _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("git status");
        lines[0].Should().Contain("Check repo");
        lines[0].Should().Contain("git");
    }

    [Test]
    public async Task WriteAsync_JsonFormat_ManySnippets_WritesOneLineEach()
    {
        IReadOnlyList<Snippet> snippets =
        [
            new Snippet("git status", null, []),
            new Snippet("docker ps", "List containers", ["docker"]),
        ];
        await CreateSut().WriteAsync(snippets, OutputFormat.Json, CancellationToken.None);

        var lines = _output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
    }

    [Test]
    public async Task WriteAsync_JsonFormat_SpecialChars_EscapesCorrectly()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("echo \"hello\"", null, [])];
        await CreateSut().WriteAsync(snippets, OutputFormat.Json, CancellationToken.None);

        var line = _output.ToString().Trim();
        // System.Text.Json encodes " as "
        (line.Contains("\\u0022") || line.Contains("\\\"")).Should().BeTrue();
    }

    [Test]
    public async Task WriteAsync_JsonFormat_NoSpectreEscapes_CleanOutput()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", "Check repo", ["git"])];
        await CreateSut().WriteAsync(snippets, OutputFormat.Json, CancellationToken.None);

        var output = _output.ToString();
        output.Should().NotContain("\x1b[");
        output.Should().NotContain("[green]");
    }

    [Test]
    public async Task WriteAsync_TextFormat_RendersTable()
    {
        var console = new TestConsole();
        _mocker.Use<Spectre.Console.IAnsiConsole>(console);
        IReadOnlyList<Snippet> snippets = [new Snippet("git status", "Check repo", ["git"])];

        await CreateSut().WriteAsync(snippets, OutputFormat.Text, CancellationToken.None);

        console.Output.Should().Contain("git status");
        console.Output.Should().Contain("Check repo");
        console.Output.Should().Contain("git");
    }
}
