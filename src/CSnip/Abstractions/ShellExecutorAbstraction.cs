using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CSnip.Abstractions;

public interface IShellExecutor
{
    Task<int> ExecuteAsync(string command, CancellationToken cancellationToken = default);
    Task<int> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

public class ShellExecutor : IShellExecutor
{
    public async Task<int> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var (shell, flag) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd.exe", "/c")
            : ("/bin/sh", "-c");

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(flag);
        startInfo.ArgumentList.Add(command);

        using var process = new Process { StartInfo = startInfo };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    public async Task<int> ExecuteAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
