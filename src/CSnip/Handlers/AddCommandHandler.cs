using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;
using Spectre.Console;

namespace CSnip.Handlers;

public class AddCommandHandler(
    IAnsiConsole console,
    ILineReader lineReader,
    ISnippetRepository repository) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public IReadOnlyList<Argument> Arguments { get; } = [Command];
        public IReadOnlyList<Option> Options { get; } = [Description, Tags];

        public static readonly Argument<string?> Command = new("command")
        {
            Description = "Command snippet to save (omit to be prompted interactively)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        public static readonly Option<string> Description = new("--description", "-d")
        {
            Description = "Description of the command snippet",
        };

        public static readonly Option<string[]> Tags = new("--tags", "-t")
        {
            Description = "Tags of the command snippet",
            Arity = ArgumentArity.ZeroOrMore,
        };
    }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var command = result.GetValue(Symbols.Command);
        var description = result.GetValue(Symbols.Description);
        var tags = result.GetValue(Symbols.Tags);

        if (string.IsNullOrWhiteSpace(command))
        {
            console.Markup("[blue]command[/] [red](required)[/]: ");
            command = lineReader.ReadLine() ?? string.Empty;

            console.Markup("[blue]description[/]: ");
            description = lineReader.ReadLine() ?? string.Empty;

            console.Markup("[blue]tags[/] [dim](comma-separated)[/]: ");
            var tagsInput = lineReader.ReadLine() ?? string.Empty;

            tags = tagsInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            console.MarkupLine("[red]Command cannot be empty.[/]");
            return 1;
        }

        var snippet = new Snippet(command, string.IsNullOrWhiteSpace(description) ? null : description, tags ?? []);
        await repository.AddAsync(snippet, cancellationToken);

        console.MarkupLine("[green]Snippet saved![/]");
        return 0;
    }
}