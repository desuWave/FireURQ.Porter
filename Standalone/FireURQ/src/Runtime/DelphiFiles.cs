global using static FireURQ.Runtime.DelphiFiles;

using System.Runtime.InteropServices;

namespace FireURQ.Runtime;

internal static class DelphiFiles
{
    private static int NameStart(string path) =>
        Math.Max(path.LastIndexOf('\\'), path.LastIndexOf(':')) + 1;

    public static string ExtractFileName(string? path)
    {
        path ??= string.Empty;
        return path[NameStart(path)..];
    }

    public static string ExtractFilePath(string? path)
    {
        path ??= string.Empty;
        return path[..NameStart(path)];
    }

    public static string ExtractFileExt(string? path)
    {
        string name = ExtractFileName(path);
        int dot = name.LastIndexOf('.');
        return dot < 0 ? string.Empty : name[dot..];
    }

    public static string ChangeFileExt(string? path, string? extension)
    {
        path ??= string.Empty;
        int dot = path.LastIndexOf('.');
        return (dot >= NameStart(path) ? path[..dot] : path) + extension;
    }

    public static string IncludeTrailingPathDelimiter(string? path) =>
        path?.EndsWith('\\') == true ? path : path + "\\";

    public static bool FileExists(string? path) => File.Exists(path);
    public static bool FileExists(AnsiString path) => FileExists(path.ToSystemText());
    public static bool DeleteFile(AnsiString path) => DeleteFile(path.ToSystemText());
    public static string IncludeTrailingPathDelimiter(AnsiString path) => IncludeTrailingPathDelimiter(path.ToSystemText());
    public static string ChangeFileExt(AnsiString path, string? extension) => ChangeFileExt(path.ToSystemText(), extension);
    public static string ChangeFileExt(AnsiString path, AnsiString extension) => ChangeFileExt(path.ToSystemText(), extension.ToSystemText());

    [DllImport("kernel32.dll", EntryPoint = "DeleteFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteFile(string path);

    public static string ParamStr(int index)
    {
        if (index == 0)
            return Environment.ProcessPath ?? string.Empty;
        string[] args = Environment.GetCommandLineArgs();
        return (uint)index < (uint)args.Length ? args[index] : string.Empty;
    }
}
