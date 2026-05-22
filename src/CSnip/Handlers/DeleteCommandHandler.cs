using System.CommandLine;
using CSnip.Persistence;
using CSnip.Services;
using Spectre.Console;

namespace CSnip.Handlers;

public class DeleteCommandHandler(
    IAnsiConsole console,
    ISnippetRepository repository,
    ISnippetPromptService snippetPromptService) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols { }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var snippets = await repository.GetAllAsync(cancellationToken);
        if (snippets.Count == 0)
        {
            console.MarkupLine("[yellow]No snippets found. Use 'add' to create one.[/]");
            return 0;
        }

        var snippet = await snippetPromptService.SelectSnippetAsync(snippets, cancellationToken);
        if (snippet is null)
            return 0;

        var deleted = await repository.DeleteAsync(snippet, cancellationToken);
        if (!deleted)
        {
            console.MarkupLine("[red]Selected snippet could not be found.[/]");
            return 1;
        }

        console.MarkupLine("[green]Snippet deleted![/]");
        return 0;
    }
}
