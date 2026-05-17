using CSnip.Models;

namespace CSnip.Services;

public interface ISnippetPromptService
{
    Task<Snippet?> SelectSnippetAsync(IEnumerable<Snippet> snippets, CancellationToken cancellationToken);
}
