using System.CommandLine;

namespace CSnip.Handlers;

public interface ICliCommandHandler
{
    Task<int> HandleAsync(
        ParseResult result,
        CancellationToken cancellationToken);
}
