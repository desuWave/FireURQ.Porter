using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.SortedStringList.{Guid.NewGuid():N}");
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

foreach (var item in new[]
{
    (Name: "Objects.cs", Source: CSharpObjectRuntime.Source),
    (Name: "StringLists.cs", Source: CSharpStringListRuntime.Source),
    (Name: "StringListCommon.cs", Source: CSharpStringListCommonRuntime.Source)
})
    File.WriteAllText(Path.Combine(directory, item.Name), item.Source, encoding);

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;

int passed = 0;
int failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition) passed++; else failed++;
}

var created = new List<Tracked>();
Tracked Make()
{
    var value = new Tracked();
    created.Add(value);
    return value;
}

var list = TStringList.Create();
Tracked zeta = Make();
Tracked alpha = Make();
list.AddObject("zeta", zeta);
list.AddObject("alpha", alpha);
list.Sorted = true;
Check("Enabling Sorted preserves complete string-object pairs",
    list[0] == "alpha" && ReferenceEquals(list.Objects[0], alpha) &&
    list[1] == "zeta" && ReferenceEquals(list.Objects[1], zeta));

Tracked beta = Make();
int inserted = list.AddObject("beta", beta);
Check("Sorted AddObject returns the inserted position",
    inserted == 1 && list[1] == "beta" &&
    ReferenceEquals(list.Objects[1], beta));

Tracked ignored = Make();
list.Duplicates = TDuplicates.dupIgnore;
int existing = list.AddObject("ALPHA", ignored);
Check("dupIgnore returns the existing index",
    existing == 0 && list.Count == 3);
Check("Ignored duplicate preserves the original object",
    list[0] == "alpha" &&
    ReferenceEquals(list.Objects[0], alpha) &&
    ignored.Destroys == 0);

Tracked accepted = Make();
list.Duplicates = TDuplicates.dupAccept;
int acceptedIndex = list.AddObject("alpha", accepted);
Check("dupAccept inserts and associates a separate entry",
    list.Count == 4 &&
    ReferenceEquals(list.Objects[acceptedIndex], accepted) &&
    list.IndexOfObject(alpha) >= 0);

list.Duplicates = TDuplicates.dupError;
string[] previousStrings = Enumerable.Range(0, list.Count)
    .Select(index => list[index]).ToArray();
TObject?[] previousObjects = Enumerable.Range(0, list.Count)
    .Select(index => list.Objects[index]).ToArray();

bool rejected = false;
try
{
    list.AddObject("ALPHA", Make());
}
catch (ArgumentException)
{
    rejected = true;
}
Check("dupError rejects an equivalent duplicate",
    rejected && list.Count == 4);
Check("Rejected insertion leaves all strings and objects unchanged",
    Enumerable.Range(0, list.Count).All(index =>
        list[index] == previousStrings[index] &&
        ReferenceEquals(list.Objects[index], previousObjects[index])));
Check("Rejected insertion does not destroy borrowed objects",
    created.All(value => value.Destroys == 0));

Tracked distinct = Make();
int distinctIndex = list.AddObject("middle", distinct);
Check("dupError permits a distinct sorted entry",
    ReferenceEquals(list.Objects[distinctIndex], distinct) &&
    list[distinctIndex] == "middle");

bool replacementRejected = false;
try
{
    list[0] = "replacement";
}
catch (NotSupportedException)
{
    replacementRejected = true;
}
Check("Sorted list rejects direct string replacement", replacementRejected);

bool insertionRejected = false;
try
{
    list.Insert(0, "manual");
}
catch (NotSupportedException)
{
    insertionRejected = true;
}
Check("Sorted list rejects manual positional insertion", insertionRejected);

Tracked reassigned = Make();
list.Objects[0] = reassigned;
Check("Sorted list permits associated-object replacement without freeing",
    ReferenceEquals(list.Objects[0], reassigned) &&
    created.All(value => value.Destroys == 0));

list.Clear();
Check("Clear retains sorting and duplicate policy",
    list.Count == 0 && list.Sorted &&
    list.Duplicates == TDuplicates.dupError);
list.Add("z");
Check("Cleared sorted list still inserts in order",
    list.Add("a") == 0 && list[0] == "a" && list[1] == "z");

list.Sorted = false;
list.Add("a");
list.Add("a");
Check("Duplicate policy does not filter unsorted insertion", list.Count == 4);
list.Sorted = true;
Check("Enabling Sorted does not remove existing duplicates", list.Count == 4);

list.Clear();
list.CaseSensitive = true;
list.Add("a");
list.Add("A");
Check("Case-sensitive dupError permits differently cased strings", list.Count == 2);
list.CaseSensitive = false;
Check("Changing comparison retains existing entries", list.Count == 2);

bool foldedDuplicateRejected = false;
try
{
    list.Add("A");
}
catch (ArgumentException)
{
    foldedDuplicateRejected = true;
}
Check("Changed comparison controls subsequent dupError checks",
    foldedDuplicateRejected && list.Count == 2);

TObject.Free(list);
Check("Sorted-list destruction preserves borrowed objects",
    created.All(value => value.Destroys == 0));
foreach (Tracked value in created)
    TObject.Free(value);
Check("Caller retains responsibility for borrowed object destruction",
    created.All(value => value.Destroys == 1 && value.Completions == 1));

Console.WriteLine($"Sorted-string-list checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TObject
{
    public int Destroys;
    public int Completions;
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