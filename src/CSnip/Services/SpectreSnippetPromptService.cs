using CSnip.Models;
using Spectre.Console;

namespace CSnip.Services;

public class SpectreSnippetPromptService(IAnsiConsole console) : ISnippetPromptService
{
    public Task<Snippet?> SelectSnippetAsync(IEnumerable<Snippet> snippets, CancellationToken cancellationToken)
    {
        var list = snippets.ToList();
        if (list.Count == 0) return Task.FromResult<Snippet?>(null);

        var prompt = new SelectionPrompt<Snippet>()
            .Title("Select a snippet:")
            .PageSize(10)
            .EnableSearch()
            .UseConverter(FormatSnippet)
            .AddChoices(list);

        var selected = console.Prompt(prompt);
        return Task.FromResult<Snippet?>(selected);
    }

    public static string FormatSnippet(Snippet snippet)
    {
        var description = (snippet.Description ?? snippet.Command).PadRight(40);
        var tags = snippet.Tags.Length > 0 ? string.Join(", ", snippet.Tags) : "(no tags)";
        return $"{description} | {tags}";
    }
}
