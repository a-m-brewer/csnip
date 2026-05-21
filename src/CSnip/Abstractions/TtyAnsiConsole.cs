using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CSnip.Abstractions;

// Wraps an IAnsiConsole so all output/rendering delegates to the inner console,
// but keyboard input goes through TtyConsoleInput instead of DefaultInput.
// On .NET 10, Console.ReadKey checks the cached IsInputRedirected flag and throws
// even after a dup2 to the TTY. TtyConsoleInput bypasses this by reading directly
// from a separately-opened /dev/tty fd via P/Invoke.
internal sealed class TtyAnsiConsole(IAnsiConsole inner) : IAnsiConsole
{
    private static readonly TtyConsoleInput TtyInput = new();

    public Profile Profile => inner.Profile;
    public IAnsiConsoleCursor Cursor => inner.Cursor;
    public IAnsiConsoleInput Input => TtyInput;
    public IExclusivityMode ExclusivityMode => inner.ExclusivityMode;
    public RenderPipeline Pipeline => inner.Pipeline;
    public void Write(IRenderable renderable) => inner.Write(renderable);
    public void Clear(bool home) => inner.Clear(home);
    public void WriteAnsi(Action<AnsiWriter> writer) => inner.WriteAnsi(writer);
}

// Reads keyboard input from /dev/tty using raw-mode P/Invoke, bypassing
// Console.ReadKey (which throws when Console.IsInputRedirected is cached true).
internal sealed class TtyConsoleInput : IAnsiConsoleInput
{
    private static readonly int _fd = OpenAndConfigureTty();

    private static int OpenAndConfigureTty()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return -1;

        var fd = TtyNative.open("/dev/tty", 2 /* O_RDWR */);
        if (fd < 0) return -1;

        if (TtyNative.tcgetattr(fd, out var saved) != 0)
        {
            TtyNative.close(fd);
            return -1;
        }

        var raw = saved;
        TtyNative.ApplyRawMode(ref raw);
        TtyNative.tcsetattr(fd, 0 /* TCSANOW */, ref raw);

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            TtyNative.tcsetattr(fd, 0, ref saved);
            TtyNative.close(fd);
        };

        return fd;
    }

    public bool IsKeyAvailable()
    {
        if (_fd < 0) return true;
        return TtyNative.Poll(_fd, 0) > 0;
    }

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        if (_fd < 0) return null;
        return TtyNative.ReadKey(_fd);
    }

    public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        => Task.Run(() => ReadKey(intercept), cancellationToken);
}

internal static class TtyNative
{
    // macOS arm64 struct termios: flags are unsigned long (8 bytes), c_cc[20], then speed_t (8 bytes).
    // Layout: 4×8 + 20 + 4(pad) + 2×8 = 72 bytes total.
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    internal unsafe struct Termios
    {
        [FieldOffset(0)]  public ulong c_iflag;
        [FieldOffset(8)]  public ulong c_oflag;
        [FieldOffset(16)] public ulong c_cflag;
        [FieldOffset(24)] public ulong c_lflag;
        // c_cc array starts at offset 32; VMIN=index 16 → offset 48, VTIME=index 17 → offset 49
        [FieldOffset(48)] public byte cc_vmin;
        [FieldOffset(49)] public byte cc_vtime;
        // 4-byte pad at offsets 52-55
        [FieldOffset(56)] public ulong c_ispeed;
        [FieldOffset(64)] public ulong c_ospeed;
    }

    // macOS c_iflag bits
    const ulong IGNBRK = 0x00000001;
    const ulong BRKINT = 0x00000002;
    const ulong PARMRK = 0x00000008;
    const ulong ISTRIP = 0x00000020;
    const ulong INLCR  = 0x00000040;
    const ulong IGNCR  = 0x00000080;
    const ulong ICRNL  = 0x00000100;
    const ulong IXON   = 0x00000200;
    // macOS c_oflag bits
    const ulong OPOST  = 0x00000001;
    // macOS c_cflag bits
    const ulong CSIZE  = 0x00000300;
    const ulong CS8    = 0x00000300;
    const ulong PARENB = 0x00001000;
    // macOS c_lflag bits
    const ulong ECHO   = 0x00000008;
    const ulong ECHONL = 0x00000010;
    const ulong ICANON = 0x00000100;
    const ulong ISIG   = 0x00000080;
    const ulong IEXTEN = 0x00000400;

    internal static void ApplyRawMode(ref Termios t)
    {
        // Only disable input processing — leave OPOST alone so output (Spectre redraws)
        // still translates \n to \r\n and keeps cursor at column 0.
        // Leave ISIG enabled so Ctrl+C delivers SIGINT and exits cleanly.
        t.c_iflag &= ~(IGNBRK | BRKINT | PARMRK | ISTRIP | INLCR | IGNCR | ICRNL | IXON);
        t.c_lflag &= ~(ECHO | ECHONL | ICANON | IEXTEN);
        t.c_cflag = (t.c_cflag & ~(CSIZE | PARENB)) | CS8;
        t.cc_vmin  = 1;
        t.cc_vtime = 0;
    }

    [DllImport("libc", EntryPoint = "open")]
    internal static extern int open(string path, int flags);

    [DllImport("libc", EntryPoint = "close")]
    internal static extern int close(int fd);

    [DllImport("libc", EntryPoint = "tcgetattr")]
    internal static extern int tcgetattr(int fd, out Termios termios);

    [DllImport("libc", EntryPoint = "tcsetattr")]
    internal static extern int tcsetattr(int fd, int optional_actions, ref Termios termios);

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    [DllImport("libc", EntryPoint = "poll")]
    private static extern int poll(ref PollFd fds, uint nfds, int timeout);

    internal static int Poll(int fd, int timeoutMs)
    {
        var pfd = new PollFd { fd = fd, events = 0x0001 /* POLLIN */ };
        return poll(ref pfd, 1, timeoutMs);
    }

    [DllImport("libc", EntryPoint = "read")]
    private static extern unsafe nint read_raw(int fd, byte* buf, nint count);

    private static unsafe int ReadByte(int fd)
    {
        byte b;
        return read_raw(fd, &b, 1) == 1 ? b : -1;
    }

    private static int ReadByteTimeout(int fd, int ms)
        => Poll(fd, ms) > 0 ? ReadByte(fd) : -1;

    internal static ConsoleKeyInfo ReadKey(int fd)
    {
        int b = ReadByte(fd);
        if (b < 0) return default;

        if (b == 0x1b) // ESC — may start an escape sequence
        {
            int b2 = ReadByteTimeout(fd, 100);
            if (b2 < 0)
                return new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false);

            // CSI: ESC [ ...
            if (b2 == '[')
            {
                int b3 = ReadByteTimeout(fd, 100);
                return b3 switch
                {
                    'A' => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow,    false, false, false),
                    'B' => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow,  false, false, false),
                    'C' => new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false),
                    'D' => new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow,  false, false, false),
                    'H' => new ConsoleKeyInfo('\0', ConsoleKey.Home,       false, false, false),
                    'F' => new ConsoleKeyInfo('\0', ConsoleKey.End,        false, false, false),
                    _   => new ConsoleKeyInfo('\x1b', ConsoleKey.Escape,   false, false, false),
                };
            }

            // SS3: ESC O ...
            if (b2 == 'O')
            {
                int b3 = ReadByteTimeout(fd, 100);
                return b3 switch
                {
                    'A' => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow,    false, false, false),
                    'B' => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow,  false, false, false),
                    'C' => new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false),
                    'D' => new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow,  false, false, false),
                    'H' => new ConsoleKeyInfo('\0', ConsoleKey.Home,       false, false, false),
                    'F' => new ConsoleKeyInfo('\0', ConsoleKey.End,        false, false, false),
                    _   => new ConsoleKeyInfo('\x1b', ConsoleKey.Escape,   false, false, false),
                };
            }

            return new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false);
        }

        return b switch
        {
            0x03 => new ConsoleKeyInfo('\x03', ConsoleKey.C,         false, false, true),  // Ctrl+C
            0x0d => new ConsoleKeyInfo('\r',   ConsoleKey.Enter,     false, false, false), // CR
            0x0a => new ConsoleKeyInfo('\n',   ConsoleKey.Enter,     false, false, false), // LF
            0x7f => new ConsoleKeyInfo('\x7f', ConsoleKey.Backspace, false, false, false), // Del/BS
            0x08 => new ConsoleKeyInfo('\x08', ConsoleKey.Backspace, false, false, false), // BS
            0x09 => new ConsoleKeyInfo('\t',   ConsoleKey.Tab,       false, false, false),
            _ => MapPrintable(b),
        };
    }

    private static ConsoleKeyInfo MapPrintable(int b)
    {
        var c = (char)b;
        ConsoleKey key;
        if      (c >= 'a' && c <= 'z') key = (ConsoleKey)(ConsoleKey.A + (c - 'a'));
        else if (c >= 'A' && c <= 'Z') key = (ConsoleKey)(ConsoleKey.A + (c - 'A'));
        else if (c >= '0' && c <= '9') key = (ConsoleKey)(ConsoleKey.D0 + (c - '0'));
        else if (c == ' ')             key = ConsoleKey.Spacebar;
        else                           key = ConsoleKey.NoName;
        bool shift = char.IsUpper(c);
        return new ConsoleKeyInfo(c, key, shift, false, false);
    }
}
