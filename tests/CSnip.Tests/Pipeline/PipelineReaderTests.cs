using AwesomeAssertions;
using CSnip.Abstractions;
using CSnip.Pipeline;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Pipeline;

[TestFixture]
public class PipelineReaderTests
{
    private AutoMocker _mocker = null!;

    [SetUp]
    public void SetUp() => _mocker = new AutoMocker();

    private PipelineReader CreateSut(string stdinContent)
    {
        _mocker.GetMock<IConsoleEnvironment>()
            .Setup(e => e.In)
            .Returns(new StringReader(stdinContent));
        return _mocker.CreateInstance<PipelineReader>();
    }

    [Test]
    public async Task ReadAllAsync_EmptyInput_ReturnsEmpty()
    {
        var result = await CreateSut("").ReadAllAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Test]
    public async Task ReadAllAsync_BlankLines_ReturnsEmpty()
    {
        var result = await CreateSut("   \n\n  ").ReadAllAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Test]
    public async Task ReadAllAsync_NdjsonHappyPath_ParsesSnippets()
    {
        var input = """
            {"Command":"git status","Description":"Check repo","Tags":["git"]}
            {"Command":"docker ps","Description":null,"Tags":[]}
            """;

        var result = await CreateSut(input).ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Command.Should().Be("git status");
        result[0].Description.Should().Be("Check repo");
        result[0].Tags.Should().BeEquivalentTo(new[] { "git" });
        result[1].Command.Should().Be("docker ps");
    }

    [Test]
    public async Task ReadAllAsync_TextLineFallback_ParsesEachLineAsCommand()
    {
        var input = "git status\ndocker ps\n";

        var result = await CreateSut(input).ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Command.Should().Be("git status");
        result[0].Description.Should().BeNull();
        result[0].Tags.Should().BeEmpty();
        result[1].Command.Should().Be("docker ps");
    }

    [Test]
    public async Task ReadAllAsync_TextWithBlankLines_SkipsBlanks()
    {
        var input = "git status\n\ndocker ps\n";

        var result = await CreateSut(input).ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Test]
    public async Task ReadAllAsync_InvalidJsonLine_SkipsLineWithWarning()
    {
        var input = """
            {"Command":"git status","Description":null,"Tags":[]}
            this is not json but starts after a valid line
            {"Command":"docker ps","Description":null,"Tags":[]}
            """;

        // The second line is not valid JSON but the first line sniff detects JSON mode.
        // Only the two valid lines should be parsed; the invalid one is skipped.
        var result = await CreateSut(input).ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Command.Should().Be("git status");
        result[1].Command.Should().Be("docker ps");
    }

    [Test]
    public async Task ReadAllAsync_JsonArrayForm_ParsesAllSnippets()
    {
        var input = """[{"Command":"git status","Description":null,"Tags":[]},{"Command":"docker ps","Description":null,"Tags":[]}]""";

        var result = await CreateSut(input).ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Command.Should().Be("git status");
        result[1].Command.Should().Be("docker ps");
    }

    [Test]
    public async Task ReadAllAsync_SingleTextLine_ReturnsOneSnippet()
    {
        var result = await CreateSut("git status").ReadAllAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Command.Should().Be("git status");
    }
}
