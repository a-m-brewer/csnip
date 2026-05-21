using System.Runtime.InteropServices;

namespace CSnip.Abstractions;

// Redirect stdin (fd 0) to the controlling terminal so interactive prompts work
// when the process receives piped stdin. Classic Unix /dev/tty pattern.
internal static class TtyRedirect
{
    internal static void ReopenStdinFromTty()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                ReopenStdinWindows();
            else
                ReopenStdinUnix();
        }
        catch
        {
            // No controlling terminal — interactive prompts won't work,
            // but at least we don't crash during setup.
        }
    }

    private static void ReopenStdinUnix()
    {
        var fd = UnixNative.open("/dev/tty", 2 /* O_RDWR */);
        if (fd < 0) return;
        try { UnixNative.dup2(fd, 0 /* STDIN_FILENO */); }
        finally { UnixNative.close(fd); }
    }

    private static void ReopenStdinWindows()
    {
        var handle = WindowsNative.CreateFileW(
            "CONIN$",
            0xC0000000u, // GENERIC_READ | GENERIC_WRITE
            0x3u,        // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero,
            3u,          // OPEN_EXISTING
            0u,
            IntPtr.Zero);
        if (handle == new IntPtr(-1)) return;
        WindowsNative.SetStdHandle(-10, handle); // STD_INPUT_HANDLE
    }
}

internal static class UnixNative
{
    [DllImport("libc", EntryPoint = "open")]
    internal static extern int open(string path, int flags);

    [DllImport("libc", EntryPoint = "dup2")]
    internal static extern int dup2(int oldfd, int newfd);

    [DllImport("libc", EntryPoint = "close")]
    internal static extern int close(int fd);
}

internal static class WindowsNative
{
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", EntryPoint = "SetStdHandle")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);
}
