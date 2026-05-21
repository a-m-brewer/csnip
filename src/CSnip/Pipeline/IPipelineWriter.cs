using CSnip.Models;

namespace CSnip.Pipeline;

public interface IPipelineWriter
{
    Task WriteAsync(IReadOnlyList<Snippet> snippets, OutputFormat format, CancellationToken cancellationToken);
}
