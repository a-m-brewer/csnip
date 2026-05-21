using CSnip.Abstractions;

namespace CSnip.Pipeline;

public class FormatResolver
{
    public OutputFormat Resolve(OutputFormat requested, IConsoleEnvironment env) =>
        requested != OutputFormat.Auto ? requested :
        env.IsOutputRedirected ? OutputFormat.Json : OutputFormat.Text;
}
