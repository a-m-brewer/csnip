using System.CommandLine;
using CSnip.Models;
using CSnip.Persistence;
using Spectre.Console;

namespace CSnip.Handlers;

public class AddCommandHandler(
    IAnsiConsole console,
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
            command = console.Prompt(
                new TextPrompt<string>("Command:"));

            description = console.Prompt(
                new TextPrompt<string>("Description:")
                    .AllowEmpty());

            var tagsInput = console.Prompt(
                new TextPrompt<string>("Tags (comma-separated):")
                    .AllowEmpty());

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