using System.CommandLine;
using CSnip.Abstractions;
using Spectre.Console;

namespace CSnip.Handlers;

public class CopyCommandHandler(
    IAnsiConsole console,
    IClipboardService clipboardService) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public IReadOnlyList<Argument> Arguments { get; } = [TextArgument];

        public static Argument<string> TextArgument { get; } = new("text")
        {
            Description = "Text to copy to the clipboard",
        };
    }

    public async Task<int> HandleAsync(
        ParseResult result,
        CancellationToken cancellationToken)
    {
        var text = result.GetValue(Symbols.TextArgument)!;

        await clipboardService.SetTextAsync(text, cancellationToken);
        console.MarkupLine("[green]Copied to clipboard![/]");

        return 0;
    }
}
