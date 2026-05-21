using System.CommandLine;
using CSnip.Abstractions;
using CSnip.Persistence;
using CSnip.Pipeline;

namespace CSnip.Handlers;

public class ListCommandHandler(
    ISnippetRepository snippetRepository,
    IPipelineWriter pipelineWriter,
    FormatResolver formatResolver,
    IConsoleEnvironment consoleEnvironment) : ICliCommandHandler
{
    public class Symbols : ICommandSymbols
    {
        public static readonly Option<OutputFormat> FormatOption = new("--format", "-f")
        {
            Description = "Output format: auto, text, or json",
            DefaultValueFactory = _ => OutputFormat.Auto,
        };

        public IReadOnlyList<Option> Options => [FormatOption];
    }

    public async Task<int> HandleAsync(ParseResult result, CancellationToken cancellationToken)
    {
        var requestedFormat = result.GetValue(Symbols.FormatOption);
        var effectiveFormat = formatResolver.Resolve(requestedFormat, consoleEnvironment);

        var snippets = await snippetRepository.GetAllAsync(cancellationToken);
        await pipelineWriter.WriteAsync(snippets, effectiveFormat, cancellationToken);
        return 0;
    }
}
