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
        
        public static Argument<string> Command = new("command")
        {
            Description = "Command Snippet to save to the database",
            Validators =
            {
                r =>
                {
                    var val = r.GetValueOrDefault<string>();
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        r.AddError("Snippet cannot be empty.");
                    }
                }
            }
        };
        
        public static Option<string> Description = new("--description", "-d")
        {
            Description = "Description of the command snippet",
        };

        public static Option<string[]> Tags = new("--tags", "-t")
        {
            Description = "Tags of the command snippet",
            Arity = ArgumentArity.ZeroOrMore,
        };
    }
    
    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var command = result.GetRequiredValue(Symbols.Command);
        var description = result.GetValue(Symbols.Description);
        var tags = result.GetValue(Symbols.Tags) ?? [];

        var snippet = new Snippet(command, description, tags);
        await repository.AddAsync(snippet, cancellationToken);

        console.MarkupLine("[green]Snippet saved![/]");
        return 0;
    }
}