using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.InterfaceList.{Guid.NewGuid():N}");
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
File.WriteAllText(Path.Combine(directory, "InterfaceLists.cs"),
    CSharpInterfaceListRuntime.Source, encoding);

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

Tracked Owned()
{
    var value = new Tracked();
    ((IInterface)value)._AddRef();
    return value;
}

IInterfaceList list = TInterfaceList.Create();
Check("New interface list is empty", list.Count == 0);
Check("Nil entries are accepted without a reference operation",
    list.Add(null) == 0 && list.Items[0] is null);
list.Clear();
Check("Clearing nil entries empties the list", list.Count == 0);

var a = Owned();
Check("Add retains one reference per cell",
    list.Add(a) == 0 && a.RefCount == 2);
Check("Items and default indexer expose the same interface",
    ReferenceEquals(list.Items[0], a) &&
    ReferenceEquals(list[0], a));
Check("Borrowed reads do not silently acquire temporary ownership",
    a.RefCount == 2);

list.Delete(0);
Check("Delete releases only the cell's reference",
    a.RefCount == 1 && a.Destroys == 0);
((IInterface)a)._Release();
Check("External owner's final release destroys the object once",
    a.Destroys == 1 && a.Completions == 1);

var duplicate = new Tracked();
list.Add(duplicate);
list.Add(duplicate);
Check("Duplicate cells retain separate references", duplicate.RefCount == 2);
list.Delete(0);
Check("Deleting one duplicate preserves the other cell",
    duplicate.RefCount == 1 && duplicate.Destroys == 0);
list.Clear();
Check("Clearing the final duplicate completes destruction once",
    duplicate.RefCount == 0 &&
    duplicate.Destroys == 1 && duplicate.Completions == 1);

var oldValue = Owned();
var newValue = Owned();
list.Add(oldValue);
list.Items[0] = newValue;
Check("Replacement retains the new value and releases the previous value",
    oldValue.RefCount == 1 && newValue.RefCount == 2 &&
    ReferenceEquals(list[0], newValue));
list[0] = newValue;
Check("Self-assignment preserves reference count", newValue.RefCount == 2);
list[0] = null;
Check("Replacing with nil releases the stored reference",
    list[0] is null && newValue.RefCount == 1);
list.Clear();
((IInterface)oldValue)._Release();
((IInterface)newValue)._Release();
Check("Former values remain controlled by their external owners",
    oldValue.Destroys == 1 && oldValue.Completions == 1 &&
    newValue.Destroys == 1 && newValue.Completions == 1);

var insertValue = Owned();
bool rejected = false;
try
{
    list.Insert(2, insertValue);
}
catch (ArgumentOutOfRangeException)
{
    rejected = true;
}
Check("Failed insertion rolls back its acquired reference",
    rejected && list.Count == 0 &&
    insertValue.RefCount == 1 && insertValue.Destroys == 0);

list.Insert(0, insertValue);
var other = Owned();
list.Insert(0, other);
Check("Insert preserves order and retains both values",
    ReferenceEquals(list[0], other) &&
    ReferenceEquals(list[1], insertValue) &&
    other.RefCount == 2 && insertValue.RefCount == 2);
Check("IndexOf uses identity despite an Equals override",
    list.IndexOf(insertValue) == 1);
list.Exchange(0, 1);
Check("Exchange preserves reference counts",
    ReferenceEquals(list[0], insertValue) &&
    ReferenceEquals(list[1], other) &&
    other.RefCount == 2 && insertValue.RefCount == 2);
Check("Remove returns the actual index and releases one reference",
    list.Remove(other) == 1 && other.RefCount == 1);
Check("Missing Remove does not release another reference",
    list.Remove(other) == -1 && other.RefCount == 1);
list.Clear();
((IInterface)insertValue)._Release();
((IInterface)other)._Release();

IInterfaceList first = TInterfaceList.Create();
IInterfaceList second = TInterfaceList.Create();
var shared = new Tracked();
first.Add(shared);
second.Add(shared);
first.Clear();
Check("Clearing one list preserves another list's retained reference",
    shared.RefCount == 1 && shared.Destroys == 0);
second.Clear();
Check("Last retaining list completes destruction exactly once",
    shared.Destroys == 1 && shared.Completions == 1);

var readValue = new Tracked();
list.Add(readValue);
IInterface callerOwned = list[0]!;
callerOwned._AddRef();
list.Clear();
Check("Explicitly retained read survives list clearing",
    readValue.RefCount == 1 && readValue.Destroys == 0);
callerOwned._Release();
Check("Retained reader controls its final release",
    readValue.Destroys == 1 && readValue.Completions == 1);

var custom = new CustomReference();
list.Add(custom);
Check("List accepts a custom IInterface implementation",
    custom.References == 1);
list.Clear();
Check("Clear calls the custom release implementation",
    custom.References == 0 && custom.FinalReleases == 1);

var parallelValue = Owned();
Parallel.For(0, 1000, _ =>
{
    list.Add(parallelValue);
    list.Remove(parallelValue);
});
Check("Parallel cell operations preserve an external owner",
    list.Count == 0 &&
    parallelValue.RefCount == 1 &&
    parallelValue.Destroys == 0);
((IInterface)parallelValue)._Release();

IInterfaceList selfOwned = TInterfaceList.Create();
selfOwned._AddRef();
var held = new Tracked();
selfOwned.Add(held);
Check("Final list-interface release releases its stored interfaces",
    selfOwned._Release() == 0 &&
    held.Destroys == 1 && held.Completions == 1);

TObject.Free((TObject)list);
TObject.Free((TObject)first);
TObject.Free((TObject)second);

Console.WriteLine($"Interface-list checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TInterfacedObject
{
    public int Destroys;
    public int Completions;

    public override bool Equals(object? other) => other is Tracked;
    public override int GetHashCode() => 0;

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

sealed class CustomReference : IInterface
{
    public int References;
    public int FinalReleases;

    public int _AddRef() => ++References;
    public int _Release()
    {
        int remaining = --References;
        if (remaining == 0)
            FinalReleases++;
        return remaining;
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