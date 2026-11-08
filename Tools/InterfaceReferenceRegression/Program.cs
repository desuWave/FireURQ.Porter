using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.InterfaceReference.{Guid.NewGuid():N}");
Directory.CreateDirectory(directory);
var encoding = new UTF8Encoding(false);

File.WriteAllText(Path.Combine(directory, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""", encoding);

File.WriteAllText(Path.Combine(directory, "Objects.cs"),
    CSharpObjectRuntime.Source, encoding);

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;

int passed = 0;
int failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition)
        passed++;
    else
        failed++;
}

var value = new Tracked();
IProbe first = value;
IInterface second = value;

Check("Interface views refer to the same object",
    ReferenceEquals(first, second) && first.Value == 17);
Check("Creating C# views does not silently change Delphi refcount",
    value.RefCount == 0);

Check("Acquisition through derived interface returns one",
    first._AddRef() == 1 && value.RefCount == 1);
Check("Acquisition through base interface shares the same counter",
    second._AddRef() == 2 && value.RefCount == 2);

Check("First interface release preserves the remaining owner",
    first._Release() == 1 &&
    value.Destroys == 0 && value.Completions == 0);
Check("Final interface release destroys and completes once",
    second._Release() == 0 &&
    value.RefCount == 0 &&
    value.Destroys == 1 && value.Completions == 1);

var retained = new Tracked();
IInterface shared = retained;
shared._AddRef();

Parallel.For(0, 10000, _ =>
{
    shared._AddRef();
    shared._Release();
});
Check("Parallel interface operations preserve a retained owner",
    retained.RefCount == 1 &&
    retained.Destroys == 0 && retained.Completions == 0);

Check("Final retained interface owner completes destruction once",
    shared._Release() == 0 &&
    retained.Destroys == 1 && retained.Completions == 1);

var direct = new Tracked();
Check("Existing direct acquisition uses the interface counter",
    direct.AcquireDirect() == 1 &&
    ((IInterface)direct)._AddRef() == 2);
Check("Direct and interface releases share lifetime behavior",
    direct.ReleaseDirect() == 1 &&
    ((IInterface)direct)._Release() == 0 &&
    direct.Destroys == 1 && direct.Completions == 1);

Console.WriteLine($"Interface-reference checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

interface IProbe : IInterface
{
    int Value { get; }
}

sealed class Tracked : TInterfacedObject, IProbe
{
    public int Value => 17;
    public int Destroys;
    public int Completions;

    public int AcquireDirect() => _AddRef();
    public int ReleaseDirect() => _Release();

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        Destroys++;
        base.Destroy();
    }

    protected override void FreeInstance()
    {
        Completions++;
        base.FreeInstance();
    }
}
""", encoding);

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
    "run", "--project", Path.Combine(directory, "Probe.csproj"),
    "--configuration", "Release", "--verbosity", "quiet"
})
    start.ArgumentList.Add(argument);

using var process = Process.Start(start);
if (process is null)
{
    Console.WriteLine("[FAIL] Generated project could not start");
    Environment.ExitCode = 1;
}
else
{
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    Console.Write(await output);
    Console.Write(await errors);
    Environment.ExitCode = process.ExitCode;
}
Console.WriteLine($"Generated compilation directory: {directory}");