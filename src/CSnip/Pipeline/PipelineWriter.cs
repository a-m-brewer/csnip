using System.Text.Json;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;
using Spectre.Console;

namespace CSnip.Pipeline;

public class PipelineWriter(IAnsiConsole console, IConsoleEnvironment consoleEnvironment) : IPipelineWriter
{
    public async Task WriteAsync(IReadOnlyList<Snippet> snippets, OutputFormat format, CancellationToken cancellationToken)
    {
        if (format == OutputFormat.Json)
        {
            foreach (var snippet in snippets)
            {
                var line = JsonSerializer.Serialize(snippet, SnippetJsonContext.Default.Snippet);
                await consoleEnvironment.Out.WriteLineAsync(line);
            }
            return;
        }

        var table = new Table();
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddColumn("Tags");

        foreach (var snippet in snippets)
        {
            table.AddRow(
                Markup.Escape(snippet.Command),
                Markup.Escape(snippet.Description ?? string.Empty),
                Markup.Escape(string.Join(", ", snippet.Tags)));
        }

        console.Write(table);
        await Task.CompletedTask;
    }
}
