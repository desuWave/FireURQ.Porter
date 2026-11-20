global using static FireURQ.Runtime.DelphiText;

namespace FireURQ.Runtime;

internal static class DelphiText
{
    // Delphi SysUtils uses the current locale for case-insensitive comparisons.
    public static bool AnsiSameText(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty,
            StringComparison.CurrentCultureIgnoreCase);

    public static string Copy(string? value, int index, int count)
    {
        if (string.IsNullOrEmpty(value) || count <= 0)
            return string.Empty;

        int start = Math.Max(1, index) - 1;
        return start >= value.Length
            ? string.Empty
            : value.Substring(start, Math.Min(count, value.Length - start));
    }

    public static AnsiString Copy(AnsiString value, int index, int count) =>
        value.Copy(index, count);

    public static int Pos(string? needle, string? value) =>
        string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(value)
            ? 0
            : value.IndexOf(needle, StringComparison.Ordinal) + 1;

    public static string Trim(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        int start = 0;
        int end = value.Length;
        while (start < end && value[start] <= ' ')
            start++;
        while (end > start && value[end - 1] <= ' ')
            end--;
        return value.Substring(start, end - start);
    }
}
