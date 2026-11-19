using System.Diagnostics;
using System.Text;

internal static class IndexedPropertyExecutionRegression
{
    public static bool Run(
        string readMethod,
        string writeMethod)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FireURQ.IndexedExecution.{Guid.NewGuid():N}");

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
            "public static class Generated\n{\n" +
            "    public static " + readMethod + "\n" +
            "    public static " + writeMethod + "\n" +
            "}\n",
            encoding);

        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            using System;
            using System.Collections.Generic;

            public interface INode
            {
                string Get_Values(string key);
                void Set_Values(string key, string value);
            }

            public sealed class TestNode : INode
            {
                private readonly Dictionary<string, string> _values = new();

                public string Get_Values(string key)
                {
                    return _values.TryGetValue(key, out string? value)
                        ? value
                        : "";
                }

                public void Set_Values(string key, string value)
                {
                    _values[key] = value;
                }
            }

            public static class Program
            {
                public static int Main()
                {
                    INode node = new TestNode();

                    Generated.WriteIndexed(node, "name", "Eva");

                    string actual = Generated.ReadIndexed(node, "name");

                    bool passed = actual == "Eva";

                    Console.WriteLine(passed
                        ? "[PASS] Generated indexed read/write executes"
                        : "[FAIL] Generated indexed read/write executes");

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
                "[FAIL] Indexed execution process could not start");
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
            $"Indexed execution directory: {directory}");

        return process.ExitCode == 0;
    }
}
