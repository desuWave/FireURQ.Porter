using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.ObjectList.{Guid.NewGuid():N}");
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
using System.Runtime.InteropServices;

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

var owned = TObjectList.Create();
Check("Object list owns objects by default", owned.OwnsObjects);
var a = new Tracked(1);
owned.Add(a);
owned.Delete(0);
Check("Owned Delete destroys and completes the object once",
    owned.Count == 0 && a.Destroys == 1 && a.Completions == 1);

var b = new Tracked(2);
var c = new Tracked(3);
owned.Add(b);
owned.Add(c);
Check("Object search uses reference identity despite Equals override",
    owned.IndexOf(c) == 1);
Check("Owned Remove destroys only the selected object",
    owned.Remove(c) == 1 && c.Destroys == 1 &&
    b.Destroys == 0 && owned.Count == 1);
Check("Missing Remove returns minus one",
    owned.Remove(c) == -1 && c.Destroys == 1);

TObject? extracted = owned.Extract(b);
Check("Owned Extract preserves the object",
    ReferenceEquals(extracted, b) &&
    owned.Count == 0 && b.Destroys == 0);
TObject.Free(extracted);
Check("Extracted object can be destroyed by its caller",
    b.Destroys == 1 && b.Completions == 1);

var old = new Tracked(4);
var replacement = new Tracked(5);
owned.Add(old);
owned.Items[0] = replacement;
Check("Owned replacement frees the previous object",
    old.Destroys == 1 && replacement.Destroys == 0 &&
    ReferenceEquals(owned[0], replacement));
owned[0] = replacement;
Check("Assigning the same object does not destroy it",
    replacement.Destroys == 0);
owned[0] = null;
Check("Replacing an owned object with nil frees it",
    replacement.Destroys == 1 && owned.Items[0] is null);
owned.Pack();
Check("Object Pack removes nil entries", owned.Count == 0);

var borrowed = TObjectList.Create(false);
var d = new Tracked(6);
borrowed.Add(d);
borrowed.Delete(0);
Check("Non-owning Delete preserves the object", d.Destroys == 0);
borrowed.Add(d);
borrowed.Clear();
Check("Non-owning Clear preserves the object", d.Destroys == 0);
borrowed.Add(d);
TObject.Free(borrowed);
Check("Non-owning destruction preserves the object", d.Destroys == 0);
TObject.Free(d);

var e = new Tracked(7);
var f = new Tracked(8);
owned.Add(e);
owned.Add(f);
owned.Move(0, 1);
owned.Exchange(0, 1);
Check("Move and Exchange do not destroy owned objects",
    ReferenceEquals(owned[0], e) &&
    ReferenceEquals(owned[1], f) &&
    e.Destroys == 0 && f.Destroys == 0);

TList baseView = owned;
baseView.Delete(0);
Check("Deletion through TList preserves ownership behavior",
    e.Destroys == 1 && f.Destroys == 0 && owned.Count == 1);
IntPtr token = baseView[0];
Check("TList view exposes a valid object handle token",
    ReferenceEquals(GCHandle.FromIntPtr(token).Target, f));
baseView.Extract(token);
Check("Extraction through TList preserves the owned object",
    owned.Count == 0 && f.Destroys == 0);
TObject.Free(f);

var toggled = TObjectList.Create(false);
var g = new Tracked(9);
toggled.Add(g);
toggled.OwnsObjects = true;
Check("Enabling ownership does not immediately destroy entries",
    g.Destroys == 0);
toggled.Clear();
Check("Changed ownership applies to subsequent Clear",
    g.Destroys == 1 && g.Completions == 1);
TObject.Free(toggled);

var controls = TObjectList.Create();
var renderOrder = TObjectList.Create(false);
var control = new Tracked(10);
controls.Add(control);
renderOrder.Insert(0, control);
controls.Extract(control);
renderOrder.Remove(control);
Check("GUI owner extraction and render-order removal preserve the control",
    controls.Count == 0 && renderOrder.Count == 0 &&
    control.Destroys == 0);
TObject.Free(control);
TObject.Free(controls);
TObject.Free(renderOrder);
Check("Detached GUI control is destroyed exactly once",
    control.Destroys == 1 && control.Completions == 1);

var sorted = TObjectList.Create();
var low = new Tracked(11);
var high = new Tracked(12);
sorted.Add(low);
sorted.Add(high);
GC.Collect();
GC.WaitForPendingFinalizers();
sorted.Sort((left, right) =>
{
    var leftObject = (Tracked)GCHandle.FromIntPtr(left).Target!;
    var rightObject = (Tracked)GCHandle.FromIntPtr(right).Target!;
    return rightObject.Value.CompareTo(leftObject.Value);
});
Check("Pointer comparator resolves the original objects across GC",
    ReferenceEquals(sorted[0], high) &&
    ReferenceEquals(sorted[1], low) &&
    low.Destroys == 0 && high.Destroys == 0);
TObject.Free(sorted);
Check("Sorted list destruction frees both objects once",
    low.Destroys == 1 && low.Completions == 1 &&
    high.Destroys == 1 && high.Completions == 1);

var last = new Tracked(13);
owned.Add(last);
TObject.Free(owned);
Check("Owned list destruction frees its remaining object once",
    last.Destroys == 1 && last.Completions == 1);

Console.WriteLine($"Object-list checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TObject
{
    public readonly int Value;
    public int Destroys;
    public int Completions;

    public Tracked(int value) => Value = value;

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