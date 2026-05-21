using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Pipeline;
using CSnip.Services;
using Spectre.Console;

namespace CSnip.Handlers;

public class ExecCommandHandler(
    IAnsiConsole console,
    IShellExecutor shellExecutor,
    ISnippetRepository snippetRepository,
    ISnippetSelectionOrchestrator selectionOrchestrator,
    IConsoleEnvironment consoleEnvironment,
    IPipelineReader pipelineReader) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols { }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        IReadOnlyList<Snippet> candidates;

        if (consoleEnvironment.IsInputRedirected)
        {
            candidates = await pipelineReader.ReadAllAsync(cancellationToken);
            if (candidates.Count == 0)
            {
                console.MarkupLine("[yellow]No snippets on stdin.[/]");
                return 0;
            }

            if (candidates.Count == 1)
            {
                var resolved = await selectionOrchestrator.ResolveCommandAsync(candidates[0], cancellationToken);
                if (resolved is null) return 0;
                return await shellExecutor.ExecuteAsync(resolved, cancellationToken);
            }
        }
        else
        {
            candidates = await snippetRepository.GetAllAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                console.MarkupLine("[yellow]No snippets found. Use 'add' to create one.[/]");
                return 0;
            }
        }

        var command = await selectionOrchestrator.ResolveSnippetCommandAsync(candidates, cancellationToken);
        if (command is null) return 0;

        return await shellExecutor.ExecuteAsync(command, cancellationToken);
    }
}
