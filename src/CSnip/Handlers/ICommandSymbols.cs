using System.CommandLine;

namespace CSnip.Handlers;

// Members referenced in result.GetValue(...) must be static so handlers use the same
// instance that was registered with the command.
public interface ICommandSymbols
{
    IReadOnlyList<Argument> Arguments => [];
    IReadOnlyList<Option> Options => [];
}
