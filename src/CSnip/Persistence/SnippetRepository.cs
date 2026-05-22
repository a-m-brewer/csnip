using System.Text.Json;
using CSnip.Models;
using CSnip.Models.Settings;
using Microsoft.Extensions.Options;

namespace CSnip.Persistence;

public class SnippetRepository(IFileSystem fileSystem, IOptions<StoreSettings> options) : ISnippetRepository
{
    private string StorePath => options.Value.SnippetsPath;

    public async Task AddAsync(Snippet snippet, CancellationToken cancellationToken)
    {
        var snippets = await LoadAsync(cancellationToken);
        snippets.Add(snippet);
        await SaveAsync(snippets, cancellationToken);
    }

    public async Task<IReadOnlyList<Snippet>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await LoadAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(Snippet existingSnippet, Snippet updatedSnippet, CancellationToken cancellationToken)
    {
        var snippets = await LoadAsync(cancellationToken);
        var index = snippets.FindIndex(snippet => SnippetsEqual(snippet, existingSnippet));
        if (index < 0)
            return false;

        snippets[index] = updatedSnippet;
        await SaveAsync(snippets, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Snippet snippet, CancellationToken cancellationToken)
    {
        var snippets = await LoadAsync(cancellationToken);
        var index = snippets.FindIndex(candidate => SnippetsEqual(candidate, snippet));
        if (index < 0)
            return false;

        snippets.RemoveAt(index);
        await SaveAsync(snippets, cancellationToken);
        return true;
    }

    private async Task<List<Snippet>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!fileSystem.FileExists(StorePath))
            return [];

        await using var stream = fileSystem.OpenRead(StorePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            SnippetJsonContext.Default.ListSnippet,
            cancellationToken) ?? [];
    }

    private async Task SaveAsync(List<Snippet> snippets, CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await using var stream = fileSystem.Create(StorePath);
        await JsonSerializer.SerializeAsync(
            stream,
            snippets,
            SnippetJsonContext.Default.ListSnippet,
            cancellationToken);
    }

    private static bool SnippetsEqual(Snippet left, Snippet right)
    {
        return left.Command == right.Command &&
               left.Description == right.Description &&
               left.Tags.SequenceEqual(right.Tags);
    }
}
