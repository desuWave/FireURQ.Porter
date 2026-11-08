using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.PointerList.{Guid.NewGuid():N}");
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
File.WriteAllText(Path.Combine(directory, "Lists.cs"),
    CSharpListRuntime.Source, encoding);

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

var list = TList.Create();
IntPtr a = new(11);
IntPtr b = new(22);
IntPtr c = new(33);

Check("New pointer list is empty", list.Count == 0);
Check("Add returns insertion indices",
    list.Add(a) == 0 && list.Add(b) == 1);
Check("Default indexer and Items expose the same entries",
    list[0] == a && list.Items[1] == b);

list.Items[0] = c;
Check("Items assignment updates the default indexer", list[0] == c);
list[0] = a;
Check("Default assignment updates Items", list.Items[0] == a);

list.Insert(1, c);
Check("Insert shifts subsequent pointer values",
    list.Count == 3 && list[0] == a && list[1] == c && list[2] == b);
list.Delete(1);
Check("Delete removes only the selected entry",
    list.Count == 2 && list[0] == a && list[1] == b);

list.Add(a);
Check("IndexOf returns the first matching pointer", list.IndexOf(a) == 0);
Check("Remove returns the removed index and keeps later duplicates",
    list.Remove(a) == 0 && list.Count == 2 &&
    list[0] == b && list[1] == a);
Check("Missing Remove returns minus one without mutation",
    list.Remove(c) == -1 && list.Count == 2);

Check("Extract returns and removes the selected pointer",
    list.Extract(b) == b && list.Count == 1 && list[0] == a);
Check("Missing Extract returns a null pointer without mutation",
    list.Extract(c) == IntPtr.Zero && list.Count == 1);

list.Clear();
list.Add(IntPtr.Zero);
list.Add(a);
list.Add(IntPtr.Zero);
list.Add(b);
list.Add(IntPtr.Zero);
list.Pack();
Check("Pack removes nil entries and preserves remaining order",
    list.Count == 2 && list[0] == a && list[1] == b);

list.Add(c);
list.Move(0, 2);
Check("Move towards the end preserves the other entries",
    list[0] == b && list[1] == c && list[2] == a);
list.Move(2, 0);
Check("Move towards the beginning preserves the other entries",
    list[0] == a && list[1] == b && list[2] == c);
list.Move(1, 1);
Check("Move to the same position leaves the list unchanged",
    list[0] == a && list[1] == b && list[2] == c);

bool invalidMoveRejected = false;
try
{
    list.Move(0, list.Count);
}
catch (ArgumentOutOfRangeException)
{
    invalidMoveRejected = true;
}
Check("Invalid Move destination is rejected before removal",
    invalidMoveRejected && list.Count == 3 &&
    list[0] == a && list[1] == b && list[2] == c);

list.Exchange(0, 2);
Check("Exchange swaps only the selected entries",
    list[0] == c && list[1] == b && list[2] == a);

list.Sort((left, right) => left.ToInt64().CompareTo(right.ToInt64()));
Check("Sort callback receives the original pointer values",
    list[0] == a && list[1] == b && list[2] == c);

list.Clear();
IntPtr allBits = new(-1);
list.Add(allBits);
Check("Pointer storage preserves every native bit",
    list[0] == allBits && list[0].ToInt64() == -1);
list.Add(IntPtr.Zero);
Check("Extract can remove a nil entry",
    list.Extract(IntPtr.Zero) == IntPtr.Zero && list.Count == 1);

list.Clear();
Check("Clear empties the list", list.Count == 0);
Check("Cleared list can be reused", list.Add(c) == 0 && list[0] == c);
TObject.Free(list);
Check("Destroy clears the list entries", list.Count == 0);

Console.WriteLine($"Pointer-list checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;
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

start.Environment["DOTNET_NOLOGO"] = "1";
start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

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