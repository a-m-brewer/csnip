namespace CSnip.Abstractions;

public interface ILineReader
{
    string? ReadLine();
}

public class ConsoleLineReader : ILineReader
{
    public string? ReadLine()
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var buffer = new List<char>();
        int cursor = 0;
        int startLeft = Console.CursorLeft;
        int startTop = Console.CursorTop;
        int w = Math.Max(1, Console.WindowWidth);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    MoveTo(startLeft, startTop, buffer.Count, w);
                    Console.WriteLine();
                    return new string(buffer.ToArray());

                case ConsoleKey.Escape:
                {
                    int oldCursor = cursor;
                    buffer.Clear();
                    cursor = 0;
                    startTop = Redraw(buffer, cursor, oldCursor, startLeft, w);
                    break;
                }

                case ConsoleKey.Backspace when cursor > 0:
                {
                    int oldCursor = cursor;
                    buffer.RemoveAt(--cursor);
                    if (cursor == buffer.Count)
                        // Deleting at end: simple erase, no scroll possible
                        Console.Write("\b \b");
                    else
                        startTop = Redraw(buffer, cursor, oldCursor, startLeft, w);
                    break;
                }

                case ConsoleKey.Delete when cursor < buffer.Count:
                    buffer.RemoveAt(cursor);
                    startTop = Redraw(buffer, cursor, cursor, startLeft, w);
                    break;

                case ConsoleKey.LeftArrow when cursor > 0:
                    MoveTo(startLeft, startTop, --cursor, w);
                    break;

                case ConsoleKey.RightArrow when cursor < buffer.Count:
                    MoveTo(startLeft, startTop, ++cursor, w);
                    break;

                case ConsoleKey.Home:
                case ConsoleKey.A when key.Modifiers == ConsoleModifiers.Control:
                    cursor = 0;
                    MoveTo(startLeft, startTop, cursor, w);
                    break;

                case ConsoleKey.End:
                case ConsoleKey.E when key.Modifiers == ConsoleModifiers.Control:
                    cursor = buffer.Count;
                    MoveTo(startLeft, startTop, cursor, w);
                    break;

                default:
                    if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0')
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        if (cursor == buffer.Count)
                        {
                            // Appending to end: write directly, then recapture startTop
                            // in case this write caused the terminal to scroll.
                            Console.Write(key.KeyChar);
                            startTop = Console.CursorTop - (startLeft + cursor) / w;
                        }
                        else
                        {
                            startTop = Redraw(buffer, cursor, cursor - 1, startLeft, w);
                        }
                    }
                    break;
            }
        }
    }

    // Redraws the buffer from startLeft/startTop and repositions the cursor.
    // terminalCursor is the buffer index where the terminal cursor currently sits
    // (before the redraw), allowing us to recompute startTop if the terminal scrolled.
    private static int Redraw(List<char> buffer, int bufferCursor, int terminalCursor,
        int startLeft, int w)
    {
        int startTop = Console.CursorTop - (startLeft + terminalCursor) / w;
        Console.SetCursorPosition(startLeft, startTop);
        Console.Write("\x1b[J");
        Console.Write(new string(buffer.ToArray()));
        MoveTo(startLeft, startTop, bufferCursor, w);
        return startTop;
    }

    private static void MoveTo(int startLeft, int startTop, int position, int w)
    {
        int abs = startLeft + position;
        Console.SetCursorPosition(abs % w, startTop + abs / w);
    }
}
