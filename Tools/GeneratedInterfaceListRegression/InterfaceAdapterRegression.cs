using System.Diagnostics;
using System.Text;

internal static class InterfaceAdapterRegression
{
    public static bool Run()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FireURQ.InterfaceAdapter.{Guid.NewGuid():N}");

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
            Path.Combine(directory, "Program.cs"),
            """
            public interface IXmlBase
            {
                object GetObject();
            }

            public interface IXmlNode : IXmlBase
            {
                void Set_Text(string value);
            }

            public partial class TXmlBase : IXmlBase
            {
                object IXmlBase.GetObject() => GetObject();

                protected virtual object GetObject() => this;
            }

            public partial class TXmlNode : TXmlBase, IXmlNode
            {
                void IXmlNode.Set_Text(string value) =>
                    Set_Text(in value);

                protected virtual void Set_Text(in string value)
                {
                    Text = value;
                }

                public string Text { get; protected set; } = "";
            }

            public sealed class TXmlElement : TXmlNode
            {
                protected override void Set_Text(in string value)
                {
                    base.Set_Text(in value);
                    Text += ":override";
                }
            }

            public static class Program
            {
                public static int Main()
                {
                    var element = new TXmlElement();

                    IXmlNode node = element;
                    IXmlBase baseReference = node;

                    bool identity = ReferenceEquals(
                        baseReference.GetObject(), element);

                    node.Set_Text("FireURQ");

                    bool dispatch =
                        element.Text == "FireURQ:override";

                    Console.WriteLine(identity
                        ? "[PASS] Inherited interface adapter preserves identity"
                        : "[FAIL] Inherited interface adapter preserves identity");

                    Console.WriteLine(dispatch
                        ? "[PASS] Const adapter preserves virtual dispatch"
                        : "[FAIL] Const adapter preserves virtual dispatch");

                    return identity && dispatch ? 0 : 1;
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
                "[FAIL] Interface adapter process could not start");
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
            $"Interface adapter compilation directory: {directory}");

        return process.ExitCode == 0;
    }
}
