using System;
using System.IO;
using System.Linq;

internal static class Program
{
    public static int Main(string[] args)
    {
        string? questFile = args.FirstOrDefault(
            argument => !argument.StartsWith("-", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(questFile))
        {
            Console.Error.WriteLine(
                "Usage: FireURQ <quest-file> [-d] [-l]");
            return 2;
        }

        bool debugMode = args.Any(
            argument => argument.Equals("-d", StringComparison.OrdinalIgnoreCase));
        bool logToFile = args.Any(
            argument => argument.Equals("-l", StringComparison.OrdinalIgnoreCase));

        var app = new Tf2Application();
        try
        {
            app.Create(Path.GetFullPath(questFile), logToFile, debugMode);
            app.Run();
            return 0;
        }
        finally
        {
            app.Destroy();
        }
    }
}
