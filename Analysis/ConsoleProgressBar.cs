namespace FireURQ.Porter.Analysis;

public sealed class ConsoleProgressBar
{
    private readonly int _width;

    public ConsoleProgressBar(
        int width = 40)
    {
        _width =
            Math.Max(
                10,
                width);
    }

    public void Report(
        int current,
        int total,
        string? label = null)
    {
        if (total <= 0)
            return;

        double progress =
            Math.Clamp(
                (double)current / total,
                0.0,
                1.0);

        int filled =
            (int)Math.Round(
                progress * _width);

        string bar =
            new string(
                '█',
                filled) +
            new string(
                '░',
                _width - filled);

        int percent =
            (int)Math.Round(
                progress * 100);

        string text =
            $"{bar} {percent,3}% " +
            $"({current}/{total})";

        if (!string.IsNullOrWhiteSpace(label))
        {
            text +=
                $" {label}";
        }

        int consoleWidth =
            Math.Max(
                1,
                Console.WindowWidth);

        int maxLength =
            Math.Max(
                1,
                consoleWidth - 1);

        if (text.Length > maxLength)
        {
            text =
                text[..maxLength];
        }

        text =
            text.PadRight(
                maxLength);

        try
        {
            Console.SetCursorPosition(
                0,
                Console.CursorTop);
        }
        catch
        {
            // Some redirected/non-interactive
            // consoles do not support cursor positioning.
        }

        Console.Write(text);
    }

    public void Complete(
        int total,
        string? label = null)
    {
        Report(
            total,
            total,
            label);

        Console.WriteLine();
    }
}

