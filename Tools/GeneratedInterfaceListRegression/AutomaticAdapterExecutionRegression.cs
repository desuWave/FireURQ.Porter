using System.Diagnostics;
using System.Text;

internal static class AutomaticAdapterExecutionRegression
{
    public static bool Run(string declarations)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FireURQ.AutomaticAdapter.{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        var encoding = new UTF8Encoding(false);

        File.WriteAllText(
            Path.Combine(directory, "Probe.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """,
            encoding);

        File.WriteAllText(
            Path.Combine(directory, "Generated.cs"),
            declarations,
            encoding);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            public class TObject
            {
            }

            public interface IInterface
            {
            }

            public class TInterfacedObject : TObject, IInterface
            {
            }

            public partial class TXmlBase
            {
                protected TObject GetObject() => this;
            }

            public static class Program
            {
                public static int Main()
                {
                    var instance = new TXmlBase();
                    IXmlBase reference = instance;

                    bool passed = ReferenceEquals(
                        reference.GetObject(), instance);

                    Console.WriteLine(passed
                        ? "[PASS] Automatically generated adapter executes"
                        : "[FAIL] Automatically generated adapter executes");

                    return passed ? 0 : 1;
                }
            }
            """,
            encoding);

        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in new[]
        {
            "run",
            "--project",
            Path.Combine(directory, "Probe.csproj"),
            "--configuration",
            "Release",
            "--verbosity",
            "quiet"
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            Console.WriteLine(
                "[FAIL] Automatic adapter process could not start");
            return false;
        }

        Task<string> output =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errors =
            process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        Console.Write(output.GetAwaiter().GetResult());
        Console.Write(errors.GetAwaiter().GetResult());

        Console.WriteLine(
            $"Automatic adapter compilation directory: {directory}");

        return process.ExitCode == 0;
    }
    public static bool RunConstAdapter(string declarations)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FireURQ.ConstAdapter.{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        var encoding = new UTF8Encoding(false);

        File.WriteAllText(
            Path.Combine(directory, "Probe.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """,
            encoding);

        File.WriteAllText(
            Path.Combine(directory, "Generated.cs"),
            declarations,
            encoding);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            public class TObject
            {
            }

            public interface IInterface
            {
            }

            public class TInterfacedObject : TObject, IInterface
            {
            }

            public partial class TXmlNode
            {
                private string _text = "";

                protected void Set_Text(in string aValue)
                {
                    _text = aValue;
                }

                public string ReadText() => _text;
            }

            public static class Program
            {
                public static int Main()
                {
                    var instance = new TXmlNode();
                    IXmlNode reference = instance;

                    reference.Set_Text("FireURQ");

                    bool passed = instance.ReadText() == "FireURQ";

                    Console.WriteLine(passed
                        ? "[PASS] Automatically generated const adapter executes"
                        : "[FAIL] Automatically generated const adapter executes");

                    return passed ? 0 : 1;
                }
            }
            """,
            encoding);

        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in new[]
        {
            "run",
            "--project",
            Path.Combine(directory, "Probe.csproj"),
            "--configuration",
            "Release",
            "--verbosity",
            "quiet"
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start);

        if (process is null)
        {
            Console.WriteLine(
                "[FAIL] Const adapter process could not start");
            return false;
        }

        Task<string> output =
            process.StandardOutput.ReadToEndAsync();

        Task<string> errors =
            process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        Console.Write(output.GetAwaiter().GetResult());
        Console.Write(errors.GetAwaiter().GetResult());

        Console.WriteLine(
            $"Const adapter compilation directory: {directory}");

        return process.ExitCode == 0;
    }
}
