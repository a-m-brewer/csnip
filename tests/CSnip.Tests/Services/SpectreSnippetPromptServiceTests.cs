using AwesomeAssertions;
using CSnip.Models;
using CSnip.Services;

namespace CSnip.Tests.Services;

[TestFixture]
public class SpectreSnippetPromptServiceTests
{
    [Test]
    public void FormatSnippet_WithDescriptionAndTags_IncludesBoth()
    {
        var snippet = new Snippet("git status", "Check repo status", ["git", "vcs"]);

        var result = SpectreSnippetPromptService.FormatSnippet(snippet);

        result.Should().Contain("Check repo status");
        result.Should().Contain("git, vcs");
    }

    [Test]
    public void FormatSnippet_NullDescription_ShowsCommand()
    {
        var snippet = new Snippet("git status", null, ["git"]);

        var result = SpectreSnippetPromptService.FormatSnippet(snippet);

        result.Should().Contain("git status");
    }

    [Test]
    public void FormatSnippet_EmptyTags_ShowsPlaceholder()
    {
        var snippet = new Snippet("git status", "Check status", []);

        var result = SpectreSnippetPromptService.FormatSnippet(snippet);

        result.Should().Contain("(no tags)");
    }

    [Test]
    public void FormatSnippet_MultipleTags_JoinsWithComma()
    {
        var snippet = new Snippet("cmd", "desc", ["a", "b", "c"]);

        var result = SpectreSnippetPromptService.FormatSnippet(snippet);

        result.Should().Contain("a, b, c");
    }

    [Test]
    public void SelectSnippetAsync_EmptyList_ReturnsNull()
    {
        var sut = new SpectreSnippetPromptService(null!);

        var result = sut.SelectSnippetAsync([], CancellationToken.None).GetAwaiter().GetResult();

        result.Should().BeNull();
    }
}
