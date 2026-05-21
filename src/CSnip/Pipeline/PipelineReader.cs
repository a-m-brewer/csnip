using System.Text.Json;
using CSnip.Abstractions;
using CSnip.Models;
using CSnip.Persistence;

namespace CSnip.Pipeline;

public class PipelineReader(IConsoleEnvironment consoleEnvironment) : IPipelineReader
{
    public async Task<IReadOnlyList<Snippet>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        string? line;
        while ((line = await consoleEnvironment.In.ReadLineAsync(cancellationToken)) is not null)
            lines.Add(line);

        var firstNonEmpty = lines.FirstOrDefault(l => l.TrimStart().Length > 0);
        if (firstNonEmpty is null)
            return [];

        var firstChar = firstNonEmpty.TrimStart()[0];
        if (firstChar == '[')
            return ParseJsonArray(lines);
        if (firstChar == '{')
            return ParseNdjson(lines);
        return ParseTextLines(lines);
    }

    private static IReadOnlyList<Snippet> ParseNdjson(List<string> lines)
    {
        var results = new List<Snippet>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var snippet = JsonSerializer.Deserialize(line, SnippetJsonContext.Default.Snippet);
                if (snippet is not null)
                    results.Add(snippet);
            }
            catch (JsonException)
            {
                Console.Error.WriteLine($"[warn] skipping invalid JSON line: {line}");
            }
        }
        return results;
    }

    private static IReadOnlyList<Snippet> ParseJsonArray(List<string> lines)
    {
        var combined = string.Join('\n', lines);
        try
        {
            var snippets = JsonSerializer.Deserialize(combined, SnippetJsonContext.Default.ListSnippet);
            return snippets ?? [];
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("[warn] failed to parse JSON array from stdin");
            return [];
        }
    }

    private static IReadOnlyList<Snippet> ParseTextLines(List<string> lines) =>
        lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new Snippet(l.Trim(), null, []))
            .ToList();
}
