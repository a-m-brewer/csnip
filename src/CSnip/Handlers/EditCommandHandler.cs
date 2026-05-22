using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;
using CSnip.Services;
using Spectre.Console;

namespace CSnip.Handlers;

public class EditCommandHandler(
    IAnsiConsole console,
    ILineReader lineReader,
    ISnippetRepository repository,
    ISnippetPromptService snippetPromptService) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public IReadOnlyList<Argument> Arguments { get; } = [Command];
        public IReadOnlyList<Option> Options { get; } = [Description, Tags];

        public static readonly Argument<string?> Command = new("command")
        {
            Description = "Updated command snippet (omit to be prompted interactively)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        public static readonly Option<string> Description = new("--description", "-d")
        {
            Description = "Updated description of the command snippet",
        };

        public static readonly Option<string[]> Tags = new("--tags", "-t")
        {
            Description = "Updated tags of the command snippet",
            Arity = ArgumentArity.ZeroOrMore,
        };
    }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var snippets = await repository.GetAllAsync(cancellationToken);
        if (snippets.Count == 0)
        {
            console.MarkupLine("[yellow]No snippets found. Use 'add' to create one.[/]");
            return 0;
        }

        var existingSnippet = await snippetPromptService.SelectSnippetAsync(snippets, cancellationToken);
        if (existingSnippet is null)
            return 0;

        var updatedSnippet = HasAnySuppliedField(result)
            ? BuildSnippetFromSuppliedFields(result, existingSnippet)
            : PromptForSnippet(existingSnippet);

        if (string.IsNullOrWhiteSpace(updatedSnippet.Command))
        {
            console.MarkupLine("[red]Command cannot be empty.[/]");
            return 1;
        }

        var updated = await repository.UpdateAsync(existingSnippet, updatedSnippet, cancellationToken);
        if (!updated)
        {
            console.MarkupLine("[red]Selected snippet could not be found.[/]");
            return 1;
        }

        console.MarkupLine("[green]Snippet updated![/]");
        return 0;
    }

    private Snippet BuildSnippetFromSuppliedFields(ParseResult result, Snippet existingSnippet)
    {
        var command = WasSpecified(result, Symbols.Command)
            ? result.GetValue(Symbols.Command) ?? string.Empty
            : existingSnippet.Command;

        var description = WasSpecified(result, Symbols.Description)
            ? NormalizeDescription(result.GetValue(Symbols.Description))
            : existingSnippet.Description;

        var tags = WasSpecified(result, Symbols.Tags)
            ? result.GetValue(Symbols.Tags) ?? []
            : existingSnippet.Tags;

        return new Snippet(command, description, tags);
    }

    private Snippet PromptForSnippet(Snippet existingSnippet)
    {
        console.Markup($"[blue]command[/] [dim](current: {Markup.Escape(existingSnippet.Command)})[/]: ");
        var commandInput = lineReader.ReadLine();
        var command = string.IsNullOrWhiteSpace(commandInput)
            ? existingSnippet.Command
            : commandInput;

        var descriptionHint = existingSnippet.Description is null
            ? "none"
            : Markup.Escape(existingSnippet.Description);
        console.Markup($"[blue]description[/] [dim](current: {descriptionHint})[/]: ");
        var descriptionInput = lineReader.ReadLine();
        var description = string.IsNullOrWhiteSpace(descriptionInput)
            ? existingSnippet.Description
            : NormalizeDescription(descriptionInput);

        var tagsHint = existingSnippet.Tags.Length == 0
            ? "none"
            : Markup.Escape(string.Join(", ", existingSnippet.Tags));
        console.Markup($"[blue]tags[/] [dim](comma-separated; current: {tagsHint})[/]: ");
        var tagsInput = lineReader.ReadLine();
        var tags = string.IsNullOrWhiteSpace(tagsInput)
            ? existingSnippet.Tags
            : tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new Snippet(command, description, tags);
    }

    private static bool HasAnySuppliedField(ParseResult result)
    {
        return WasSpecified(result, Symbols.Command) ||
               WasSpecified(result, Symbols.Description) ||
               WasSpecified(result, Symbols.Tags);
    }

    private static bool WasSpecified(ParseResult result, Argument argument)
    {
        return result.GetResult(argument) is { Implicit: false };
    }

    private static bool WasSpecified(ParseResult result, Option option)
    {
        return result.GetResult(option) is { Implicit: false };
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description;
    }
}
