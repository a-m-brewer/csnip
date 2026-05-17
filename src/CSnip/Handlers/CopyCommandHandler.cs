using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Persistence;
using CSnip.Services;
using Spectre.Console;

namespace CSnip.Handlers;

public class CopyCommandHandler(
    IAnsiConsole console,
    IClipboardService clipboardService,
    ISnippetRepository snippetRepository,
    ISnippetSelectionOrchestrator selectionOrchestrator) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols { }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var snippets = await snippetRepository.GetAllAsync(cancellationToken);

        if (snippets.Count == 0)
        {
            console.MarkupLine("[yellow]No snippets found. Use 'add' to create one.[/]");
            return 0;
        }

        var command = await selectionOrchestrator.ResolveSnippetCommandAsync(snippets, cancellationToken);
        if (command is null) return 0;

        await clipboardService.SetTextAsync(command, cancellationToken);
        console.MarkupLine("[green]Copied to clipboard![/]");
        return 0;
    }
}
