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
}
