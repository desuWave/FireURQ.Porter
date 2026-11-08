using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.StringList.{Guid.NewGuid():N}");
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
File.WriteAllText(Path.Combine(directory, "StringLists.cs"),
    CSharpStringListRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "StringListCommon.cs"),
    CSharpStringListCommonRuntime.Source, encoding);

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

var created = new List<Tracked>();
Tracked Make(int z)
{
    var value = new Tracked(z);
    created.Add(value);
    return value;
}

var list = TStringList.Create();
Check("New TStringList is empty", list.Count == 0);
Tracked a = Make(1);
Tracked b = Make(2);
Tracked replacement = Make(3);
Check("AddObject returns insertion indices",
    list.AddObject("zeta", a) == 0 &&
    list.AddObject("alpha", b) == 1);
Check("Strings and default indexer share the same entries",
    list[0] == "zeta" && list.Strings[1] == "alpha");
Check("Default search ignores case", list.IndexOf("ALPHA") == 1);
list.CaseSensitive = true;
Check("Case-sensitive search distinguishes case",
    list.IndexOf("ALPHA") == -1 && list.IndexOf("alpha") == 1);
list.CaseSensitive = false;
Check("Object search uses reference identity",
    list.IndexOfObject(b) == 1);

list.Sort();
Check("Default Sort preserves string-object associations",
    list[0] == "alpha" && ReferenceEquals(list.Objects[0], b) &&
    list[1] == "zeta" && ReferenceEquals(list.Objects[1], a));
list.Strings[0] = "renamed";
Check("Renaming retains the associated object",
    ReferenceEquals(list.Objects[0], b));
list.Objects[0] = replacement;
Check("Object replacement does not destroy the previous object",
    b.Destroys == 0 &&
    ReferenceEquals(list.Objects[0], replacement));
list.Delete(0);
Check("Base Delete does not destroy the associated object",
    replacement.Destroys == 0 && list.Count == 1);
list.Clear();
Check("Base Clear does not destroy associated objects",
    a.Destroys == 0 && list.Count == 0);

list.Add("\u041C\u0435\u0442\u043A\u0430");
Check("Case-insensitive lookup supports Cyrillic",
    list.IndexOf("\u041C\u0415\u0422\u041A\u0410") == 0);
list.Clear();

Tracked inserted = Make(4);
list.AddObject("last", a);
list.InsertObject(0, "first", inserted);
Check("InsertObject shifts complete entries",
    list[0] == "first" && ReferenceEquals(list.Objects[0], inserted) &&
    list[1] == "last" && ReferenceEquals(list.Objects[1], a));
list.Exchange(0, 1);
Check("Exchange swaps complete entries",
    list[0] == "last" && ReferenceEquals(list.Objects[0], a) &&
    list[1] == "first" && ReferenceEquals(list.Objects[1], inserted));

var ordered = TStringList.Create();
var expected = new Dictionary<string, Tracked>();
var random = new Random(1234);
for (int index = 0; index < 128; index++)
{
    string name = "item-" + index;
    Tracked value = Make(random.Next(-15, 16));
    expected.Add(name, value);
    ordered.AddObject(name, value);
}

bool validIndices = true;
ordered.CustomSort((current, first, second) =>
{
    if (first < 0 || second < 0 ||
        first >= current.Count || second >= current.Count)
    {
        validIndices = false;
        return 0;
    }
    var firstObject = (Tracked)current.Objects[first]!;
    var secondObject = (Tracked)current.Objects[second]!;
    return secondObject.Z.CompareTo(firstObject.Z);
});
Check("CustomSort callbacks receive valid current indices", validIndices);
Check("CustomSort orders objects by descending Z",
    Enumerable.Range(1, ordered.Count - 1).All(index =>
        ((Tracked)ordered.Objects[index - 1]!).Z >=
        ((Tracked)ordered.Objects[index]!).Z));
Check("CustomSort preserves every original string-object pair",
    Enumerable.Range(0, ordered.Count).All(index =>
        ReferenceEquals(expected[ordered[index]], ordered.Objects[index])));
Check("Sorting does not destroy associated objects",
    created.All(value => value.Destroys == 0));

TObject.Free(ordered);
TObject.Free(list);
Check("Base destruction leaves borrowed objects alive",
    created.All(value => value.Destroys == 0));
foreach (Tracked value in created)
    TObject.Free(value);
Check("Caller can destroy every borrowed object once",
    created.All(value => value.Destroys == 1 && value.Completions == 1));

var decorators = new DecoratorLikeList();
var low = new Tracked(-2);
var high = new Tracked(7);
decorators.AddObject("low", low);
decorators.AddObject("high", high);
decorators.Sort();
Check("Descendant Sort can order by associated objects",
    ReferenceEquals(decorators.Objects[0], high) &&
    ReferenceEquals(decorators.Objects[1], low));

TStrings baseView = decorators;
baseView.Delete(0);
Check("Deletion through TStrings dispatches to the descendant",
    decorators.DeleteCalls == 1 && high.Destroys == 1 &&
    high.Completions == 1 && low.Destroys == 0);
baseView.Clear();
Check("Descendant Clear frees its objects once",
    decorators.ClearCalls == 1 &&
    decorators.DeleteCalls == 1 &&
    low.Destroys == 1 && low.Completions == 1);

var last = new Tracked(0);
decorators.AddObject("last", last);
TObject.Free(decorators);
Check("Inherited destruction does not repeat descendant cleanup",
    decorators.ClearCalls == 1 &&
    decorators.DeleteCalls == 1 &&
    last.Destroys == 1 && last.Completions == 1 &&
    high.Destroys == 1 && low.Destroys == 1);

Console.WriteLine($"String-list checks: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TObject
{
    public readonly int Z;
    public int Destroys;
    public int Completions;
    public Tracked(int z) => Z = z;
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

sealed class DecoratorLikeList : TStringList
{
    public int DeleteCalls;
    public int ClearCalls;

    private void ClearObjects()
    {
        for (int index = 0; index < Count; index++)
            TObject.Free(Objects[index]);
    }

    public override void Delete(int index)
    {
        DeleteCalls++;
        TObject.Free(Objects[index]);
        base.Delete(index);
    }

    public override void Clear()
    {
        ClearCalls++;
        ClearObjects();
        base.Clear();
    }

    public override void Sort()
    {
        CustomSort((list, first, second) =>
            ((Tracked)list.Objects[second]!).Z.CompareTo(
                ((Tracked)list.Objects[first]!).Z));
    }

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        ClearObjects();
        base.Destroy();
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