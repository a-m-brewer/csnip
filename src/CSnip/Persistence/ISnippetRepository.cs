using CSnip.Models;

namespace CSnip.Persistence;

public interface ISnippetRepository
{
    Task AddAsync(Snippet snippet, CancellationToken cancellationToken);
    Task<IReadOnlyList<Snippet>> GetAllAsync(CancellationToken cancellationToken);
}
