global using static FireURQ.Runtime.DelphiText;

namespace FireURQ.Runtime;

internal static class DelphiText
{
    // Delphi SysUtils uses the current locale for case-insensitive comparisons.
    public static bool AnsiSameText(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty,
            StringComparison.CurrentCultureIgnoreCase);

    public static string AnsiLowerCase(string? value) =>
        (value ?? string.Empty).ToLower(System.Globalization.CultureInfo.CurrentCulture);

    public static bool AnsiStartsText(string? prefix, string? value)
    {
        prefix ??= string.Empty;
        value ??= string.Empty;
        return prefix.Length <= value.Length &&
            string.Compare(prefix, 0, value, 0, prefix.Length,
                StringComparison.CurrentCultureIgnoreCase) == 0;
    }

    // Delphi 7 LowerCase folds only the ASCII letters A..Z.
    public static string LowerCase(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return string.Create(value.Length, value, static (result, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char c = source[index];
                result[index] = c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;
            }
        });
    }

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

    public static string AnsiUpperCase(string? value) =>
        (value ?? string.Empty).ToUpper(System.Globalization.CultureInfo.CurrentCulture);

    public static bool AnsiStartsStr(string? prefix, string? value) =>
        (value ?? string.Empty).StartsWith(prefix ?? string.Empty, StringComparison.Ordinal);

    public static int PosEx(string? needle, string? value, int offset = 1)
    {
        if (string.IsNullOrEmpty(needle) || string.IsNullOrEmpty(value) || offset < 1 || offset > value.Length)
            return 0;
        return value.IndexOf(needle, offset - 1, StringComparison.Ordinal) + 1;
    }

    public static string TrimLeft(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        int start = 0;
        while (start < value.Length && value[start] <= ' ')
            start++;
        return value[start..];
    }

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

    public static char DecimalSeparator { get; set; } =
        System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];

    public static string IntToStr(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string IntToHex(long value, int digits) =>
        unchecked((ulong)value).ToString($"X{digits}", System.Globalization.CultureInfo.InvariantCulture);

    public static int StrToIntDef(string? value, int defaultValue)
    {
        string text = (value ?? string.Empty).Trim();
        bool negative = text.StartsWith("-", StringComparison.Ordinal);
        if (negative || text.StartsWith("+", StringComparison.Ordinal))
            text = text[1..];

        System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Integer;
        if (text.StartsWith("$", StringComparison.Ordinal))
        {
            text = text[1..];
            style = System.Globalization.NumberStyles.AllowHexSpecifier;
        }

        if (!int.TryParse(text, style, System.Globalization.CultureInfo.InvariantCulture, out int result))
            return defaultValue;
        return negative ? unchecked(-result) : result;
    }

    public static double StrToFloatDef(string? value, double defaultValue)
    {
        string text = value ?? string.Empty;
        if (DecimalSeparator != '.')
            text = text.Replace(DecimalSeparator, '.');
        return double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double result)
            ? result : defaultValue;
    }

    public static string UpperCase(string? value) =>
        (value ?? string.Empty).ToUpperInvariant();

    public static string Format(string format, object?[] values)
    {
        var result = new System.Text.StringBuilder();
        int argument = 0;

        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%')
            {
                result.Append(format[index]);
                continue;
            }

            index++;
            if (index < format.Length && format[index] == '%')
            {
                result.Append('%');
                continue;
            }

            bool leftAligned = false;
            if (index < format.Length && format[index] == '-')
            {
                leftAligned = true;
                index++;
            }

            int width = 0;
            while (index < format.Length && char.IsDigit(format[index]))
                width = checked(width * 10 + format[index++] - '0');

            int precision = -1;
            if (index < format.Length && format[index] == '.')
            {
                index++;
                precision = 0;
                while (index < format.Length && char.IsDigit(format[index]))
                    precision = checked(precision * 10 + format[index++] - '0');
            }

            if (index >= format.Length || argument >= values.Length)
                throw new FormatException("Invalid Delphi format string.");

            object? value = values[argument++];
            char specifier = format[index];
            string formatted = specifier switch
            {
                'd' => FormatDecimal(value, precision),
                'u' => Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
                'x' => Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(precision >= 0 ? $"x{precision}" : "x",
                        System.Globalization.CultureInfo.InvariantCulture),
                'X' => Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(precision >= 0 ? $"X{precision}" : "X",
                        System.Globalization.CultureInfo.InvariantCulture),
                's' => Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
                'f' => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(precision >= 0 ? $"F{precision}" : "F",
                        System.Globalization.CultureInfo.CurrentCulture),
                _ => throw new FormatException($"Unsupported Delphi format specifier %{specifier}.")
            };

            if (specifier == 's' && precision >= 0 && formatted.Length > precision)
                formatted = formatted[..precision];

            result.Append(width > formatted.Length
                ? leftAligned ? formatted.PadRight(width) : formatted.PadLeft(width)
                : formatted);
        }

        return result.ToString();
    }

    private static string FormatDecimal(object? value, int precision)
    {
        long number = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        if (precision < 0)
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string sign = number < 0 ? "-" : string.Empty;
        string digits = Math.Abs(number).ToString(
            $"D{precision}", System.Globalization.CultureInfo.InvariantCulture);
        return sign + digits;
    }

    public static string StringReplace(
        string? source, string? oldValue, string? newValue, HashSet<int> flags)
    {
        source ??= string.Empty;
        oldValue ??= string.Empty;
        newValue ??= string.Empty;
        if (oldValue.Length == 0)
            return source;

        StringComparison comparison = flags.Contains(rfIgnoreCase)
            ? StringComparison.CurrentCultureIgnoreCase
            : StringComparison.Ordinal;
        int start = source.IndexOf(oldValue, comparison);
        if (start < 0)
            return source;

        var result = new System.Text.StringBuilder();
        int copied = 0;
        do
        {
            result.Append(source, copied, start - copied);
            result.Append(newValue);
            copied = start + oldValue.Length;
            if (!flags.Contains(rfReplaceAll))
                break;
            start = source.IndexOf(oldValue, copied, comparison);
        }
        while (start >= 0);

        result.Append(source, copied, source.Length - copied);
        return result.ToString();
    }

    public static bool StrMatches(string? value, string? pattern)
    {
        value ??= string.Empty;
        pattern ??= string.Empty;
        int valueIndex = 0;
        int patternIndex = 0;
        int star = -1;
        int retry = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                star = patternIndex++;
                retry = valueIndex;
            }
            else if (star >= 0)
            {
                patternIndex = star + 1;
                valueIndex = ++retry;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;
        return patternIndex == pattern.Length;
    }
}
