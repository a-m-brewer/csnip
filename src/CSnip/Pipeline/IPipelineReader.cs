using CSnip.Models;

namespace CSnip.Pipeline;

public interface IPipelineReader
{
    Task<IReadOnlyList<Snippet>> ReadAllAsync(CancellationToken cancellationToken);
}
