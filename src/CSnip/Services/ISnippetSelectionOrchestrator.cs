using CSnip.Models;

namespace CSnip.Services;

public interface ISnippetSelectionOrchestrator
{
    Task<string?> ResolveSnippetCommandAsync(
        IEnumerable<Snippet> snippets,
        CancellationToken cancellationToken);
}
