namespace FireURQ.Porter.Translation;

public static class CSharpD2dLogFormatRuntime
{
    public const string Source = """
using System.Globalization;
using System.Text.RegularExpressions;

namespace FireURQ.Runtime;

public static class D2dLogFormat
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetACP();

    public static void AppendLine(string path, string message)
    {
        try
        {
            System.Text.Encoding.RegisterProvider(
                System.Text.CodePagesEncodingProvider.Instance);
            var encoding = System.Text.Encoding.GetEncoding((int)GetACP());
            byte[] bytes = encoding.GetBytes(message + "\r\n");

            using var stream = new System.IO.FileStream(
                path, System.IO.FileMode.OpenOrCreate,
                System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
            stream.Seek(0, System.IO.SeekOrigin.End);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (System.IO.IOException) { }
        catch (System.UnauthorizedAccessException) { }
        catch (System.ArgumentException) { }
        catch (System.NotSupportedException) { }
    }

    public static string Format(string format, object[] args)
    {
        int index = 0;
        string escaped = format.Replace("{", "{{").Replace("}", "}}");
        string composite = Regex.Replace(escaped, "%([sSdDxX%])", match =>
        {
            char specifier = char.ToUpperInvariant(match.Value[1]);
            if (specifier == '%')
                return "%";

            string argumentIndex = (index++).ToString(CultureInfo.InvariantCulture);
            return specifier switch
            {
                'D' => "{" + argumentIndex + ":D}",
                'X' => "{" + argumentIndex + ":X}",
                _ => "{" + argumentIndex + "}"
            };
        });

        return string.Format(CultureInfo.CurrentCulture, composite, args);
    }
}
""";
}