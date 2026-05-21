namespace CSnip.Abstractions;

public interface IConsoleEnvironment
{
    bool IsInputRedirected { get; }
    bool IsOutputRedirected { get; }
    TextReader In { get; }
    TextWriter Out { get; }
}

// Values are captured at construction so they remain stable after any TTY redirection.
public class ConsoleEnvironmentAbstraction(
    bool isInputRedirected,
    bool isOutputRedirected,
    TextReader @in,
    TextWriter @out) : IConsoleEnvironment
{
    public bool IsInputRedirected { get; } = isInputRedirected;
    public bool IsOutputRedirected { get; } = isOutputRedirected;
    public TextReader In { get; } = @in;
    public TextWriter Out { get; } = @out;
}
