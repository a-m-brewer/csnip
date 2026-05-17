using AwesomeAssertions;
using CSnip.Models;
using CSnip.Services;
using Moq;
using Moq.AutoMock;

namespace CSnip.Tests.Services;

[TestFixture]
public class SnippetSelectionOrchestratorTests
{
    private AutoMocker _mocker = null!;
    private SnippetSelectionOrchestrator _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _mocker = new AutoMocker();
        _sut = _mocker.CreateInstance<SnippetSelectionOrchestrator>();
    }

    [Test]
    public async Task ResolveSnippetCommandAsync_NoSnippetSelected_ReturnsNull()
    {
        var snippets = new[] { new Snippet("git status", null, []) };
        _mocker.GetMock<ISnippetPromptService>()
            .Setup(s => s.SelectSnippetAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snippet?)null);

        var result = await _sut.ResolveSnippetCommandAsync(snippets, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveSnippetCommandAsync_SnippetWithNoTemplates_ReturnsCommandDirectly()
    {
        var snippet = new Snippet("git status", "Show status", []);
        _mocker.GetMock<ISnippetPromptService>()
            .Setup(s => s.SelectSnippetAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippet);
        _mocker.GetMock<ITemplateService>()
            .Setup(s => s.ParseTemplates(snippet.Command))
            .Returns([]);

        var result = await _sut.ResolveSnippetCommandAsync([snippet], CancellationToken.None);

        result.Should().Be("git status");
        _mocker.GetMock<ITemplatePromptService>()
            .Verify(s => s.PromptForTemplatesAsync(It.IsAny<IReadOnlyList<TemplateVariable>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ResolveSnippetCommandAsync_SnippetWithTemplates_PromptsAndReturnsResolvedCommand()
    {
        var snippet = new Snippet("ssh <user=admin>@<hostname>", null, []);
        TemplateVariable[] templates = [new("user", "admin"), new("hostname", null)];
        Dictionary<string, string> values = new() { ["user"] = "root", ["hostname"] = "server1" };

        _mocker.GetMock<ISnippetPromptService>()
            .Setup(s => s.SelectSnippetAsync(It.IsAny<IEnumerable<Snippet>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snippet);
        _mocker.GetMock<ITemplateService>()
            .Setup(s => s.ParseTemplates(snippet.Command))
            .Returns(templates);
        _mocker.GetMock<ITemplatePromptService>()
            .Setup(s => s.PromptForTemplatesAsync(templates, It.IsAny<CancellationToken>()))
            .ReturnsAsync(values);
        _mocker.GetMock<ITemplateService>()
            .Setup(s => s.ApplyTemplates(snippet.Command, values))
            .Returns("ssh root@server1");

        var result = await _sut.ResolveSnippetCommandAsync([snippet], CancellationToken.None);

        result.Should().Be("ssh root@server1");
    }

    [Test]
    public async Task ResolveSnippetCommandAsync_PassesAllSnippetsToPromptService()
    {
        Snippet[] snippets = [new("cmd1", null, []), new("cmd2", null, [])];
        _mocker.GetMock<ISnippetPromptService>()
            .Setup(s => s.SelectSnippetAsync(snippets, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snippet?)null);

        await _sut.ResolveSnippetCommandAsync(snippets, CancellationToken.None);

        _mocker.GetMock<ISnippetPromptService>()
            .Verify(s => s.SelectSnippetAsync(snippets, It.IsAny<CancellationToken>()), Times.Once);
    }
}
