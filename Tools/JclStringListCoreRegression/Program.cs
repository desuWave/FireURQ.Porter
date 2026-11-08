using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Translation;

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.JclCore.{Guid.NewGuid():N}");
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
File.WriteAllText(Path.Combine(directory, "Variants.cs"),
    CSharpVariantRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "Lists.cs"),
    CSharpJclStringListRuntime.Source, encoding);
File.WriteAllText(Path.Combine(directory, "StringListCommon.cs"),
    CSharpStringListCommonRuntime.Source, encoding);

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;
using static FireURQ.Runtime.DelphiVariants;

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

IJclStringList list = new TJclStringList();
Check("New list has no elements", list.Count == 0 && list.LastIndex == -1);

int second = list.Add("beta");
int first = list.Add("alpha");
Check("Add returns insertion indices", second == 0 && first == 1);
Check("Strings retain insertion order",
    list[0] == "beta" && list.Strings[1] == "alpha");
Check("New associated values are Unassigned",
    VarIsEmpty(list.Variants[0]) && VarIsEmpty(list.Variants[1]));

list.Variants[second] = 20;
list.Variants[first] = 10;
Check("Associated values are independent",
    (double)list.Variants[0] == 20 &&
    (double)list.Variants[1] == 10);

Check("Default search ignores case",
    list.IndexOf("ALPHA") == 1);
list.CaseSensitive = true;
Check("Case-sensitive search distinguishes case",
    list.IndexOf("ALPHA") == -1 && list.IndexOf("alpha") == 1);
list.CaseSensitive = false;

list.Sort();
Check("Sort preserves string-value associations",
    list[0] == "alpha" && (double)list.Variants[0] == 10 &&
    list[1] == "beta" && (double)list.Variants[1] == 20);

list.Strings[0] = "renamed";
Check("Renaming preserves its associated value",
    list[0] == "renamed" && (double)list.Variants[0] == 10);

list.KeyVariant["BETA"] = "changed";
Check("Key assignment updates an existing case-insensitive match",
    list.Count == 2 && (string)list.Variants[1] == "changed");

list.KeyVariant["new"] = 7;
Check("Key assignment creates a missing entry",
    list.Count == 3 && (double)list.KeyVariant["NEW"] == 7);
Check("Missing key returns Unassigned without insertion",
    VarIsEmpty(list.KeyVariant["missing"]) && list.Count == 3);

list.Delete(0);
Check("Delete keeps remaining strings and values aligned",
    list.Count == 2 &&
    list[0] == "beta" && (string)list.Variants[0] == "changed" &&
    list[1] == "new" && (double)list.Variants[1] == 7);

list.Variants[0] = Null;
Check("Null remains distinct from Unassigned",
    VarIsNull(list.Variants[0]) && !VarIsEmpty(list.Variants[0]));

list.Clear();
Check("Clear resets count and final index",
    list.Count == 0 && list.LastIndex == -1);
int reused = list.Add("fresh");
Check("Reused list does not retain old associated values",
    reused == 0 && VarIsEmpty(list.Variants[0]));

list.Add("fresh");
Check("Unsorted insertion permits duplicate strings", list.Count == 2);
Check("Search returns the first matching index", list.IndexOf("fresh") == 0);

list.Clear();
list.Add("\u041C\u0435\u0442\u043A\u0430");
Check("Case-insensitive search supports Cyrillic",
    list.IndexOf("\u041C\u0415\u0422\u041A\u0410") == 0);

list.Clear();
list.Sorted = false;
list.Duplicates = TDuplicates.dupIgnore;
list.Add("zeta");
list.Variants[0] = 90;
list.Add("alpha");
list.Variants[1] = 10;
list.Sorted = true;
Check("Enabling Sorted orders complete entries",
    list[0] == "alpha" && (double)list.Variants[0] == 10 &&
    list[1] == "zeta" && (double)list.Variants[1] == 90);

int middle = list.Add("middle");
Check("Sorted Add returns the inserted position",
    middle == 1 && list[1] == "middle");
Check("Sorted insertion preserves shifted associated values",
    (double)list.Variants[0] == 10 &&
    VarIsEmpty(list.Variants[1]) &&
    (double)list.Variants[2] == 90);

list.Variants[middle] = 50;
int ignored = list.Add("MIDDLE");
Check("dupIgnore returns the existing index",
    ignored == 1 && list.Count == 3);
Check("Ignoring a duplicate preserves its text and value",
    list[ignored] == "middle" &&
    (double)list.Variants[ignored] == 50);

list.KeyVariant["MIDDLE"] = 55;
Check("Sorted key update preserves element count",
    list.Count == 3 && (double)list.KeyVariant["middle"] == 55);
list.KeyVariant["beta"] = 20;
Check("Sorted key insertion associates the new value correctly",
    list.IndexOf("beta") == 1 &&
    (double)list.KeyVariant["beta"] == 20 &&
    (double)list.KeyVariant["zeta"] == 90);

list.Duplicates = TDuplicates.dupAccept;
int accepted = list.Add("middle");
Check("dupAccept inserts another matching string",
    list.Count == 5 && list[accepted] == "middle" &&
    VarIsEmpty(list.Variants[accepted]));
Check("Accepted duplicate does not overwrite the previous value",
    Enumerable.Range(0, list.Count).Count(
        index => list[index] == "middle" &&
                 !VarIsEmpty(list.Variants[index]) &&
                 (double)list.Variants[index] == 55) == 1);

list.Sorted = false;
int appendedDuplicate = list.Add("alpha");
Check("Disabling Sorted restores append semantics",
    appendedDuplicate == 5 && list[5] == "alpha");
list.Duplicates = TDuplicates.dupIgnore;
list.Add("alpha");
Check("dupIgnore does not filter an unsorted insertion",
    list.Count == 7);

list.Sorted = true;
Check("Enabling Sorted retains existing duplicates",
    list.Count == 7 &&
    Enumerable.Range(0, list.Count).Count(
        index => list[index] == "alpha") == 3);

string original = list[0];
Variant originalValue = list.Variants[0];
bool rejected = false;
try
{
    list.Strings[0] = "replacement";
}
catch (NotSupportedException)
{
    rejected = true;
}
Check("Sorted list rejects direct string replacement",
    rejected && list[0] == original &&
    list.Variants[0] == originalValue);

list.Variants[0] = 123;
Check("Sorted list permits associated-value changes",
    (double)list.Variants[0] == 123);

list.Clear();
list.Sorted = false;
list.CaseSensitive = true;
list.Duplicates = TDuplicates.dupIgnore;
list.Add("a");
list.Variants[0] = 1;
list.Add("A");
list.Variants[1] = 2;
list.Sorted = true;
Check("Case-sensitive sorted list retains distinct case",
    list.Count == 2 &&
    (double)list.Variants[list.IndexOf("a")] == 1 &&
    (double)list.Variants[list.IndexOf("A")] == 2);

list.CaseSensitive = false;
Check("Changing comparison retains existing entries",
    list.Count == 2);
int caseDuplicate = list.Add("A");
Check("Changed comparison controls subsequent duplicates",
    list.Count == 2 && caseDuplicate == list.IndexOf("a"));

list.Clear();
Check("Clear retains sorting and duplicate settings",
    list.Sorted &&
    list.Duplicates == TDuplicates.dupIgnore &&
    !list.CaseSensitive &&
    list.Count == 0);
list.Add("z");
int resetIndex = list.Add("a");
Check("Cleared sorted list still inserts in order",
    resetIndex == 0 && list[0] == "a" && list[1] == "z" &&
    VarIsEmpty(list.Variants[0]) &&
    VarIsEmpty(list.Variants[1]));

IJclStringList references = new TJclStringList();
references.Add("z");
references.Add("a");
Check("Plain strings do not select an associated-data mode",
    references.ObjectsMode == TJclStringListObjectsMode.omNone);
Check("Empty interface cell returns null and selects interface mode",
    references.Interfaces[0] is null &&
    references.ObjectsMode == TJclStringListObjectsMode.omInterfaces);

object zReference = new();
object aReference = new();
references.Interfaces[0] = zReference;
references.Interfaces[1] = aReference;
references.Sorted = true;
Check("Sorting preserves interface associations",
    references[0] == "a" &&
    ReferenceEquals(references.Interfaces[0], aReference) &&
    ReferenceEquals(references.Interfaces[1], zReference));

references.Interfaces[0] = null;
Check("Clearing one interface cell preserves another",
    references.Interfaces[0] is null &&
    ReferenceEquals(references.Interfaces[1], zReference));

references.Delete(0);
Check("Deleting an entry preserves the remaining interface association",
    references.Count == 1 &&
    references[0] == "z" &&
    ReferenceEquals(references.Interfaces[0], zReference));

references.Sorted = false;
references[0] = "renamed";
Check("Renaming preserves an interface association",
    ReferenceEquals(references.Interfaces[0], zReference));

bool mixedReadRejected = false;
try
{
    Variant incompatible = references.Variants[0];
}
catch (InvalidCastException)
{
    mixedReadRejected = true;
}
Check("Interface mode rejects Variant access without changing data",
    mixedReadRejected &&
    references.ObjectsMode == TJclStringListObjectsMode.omInterfaces &&
    ReferenceEquals(references.Interfaces[0], zReference));

references.Clear();
references.Add("new");
Check("Clear retains interface mode and removes previous cell data",
    references.ObjectsMode == TJclStringListObjectsMode.omInterfaces &&
    references.Interfaces[0] is null);

IJclStringList valuesOnly = new TJclStringList();
valuesOnly.Add("value");
valuesOnly.Variants[0] = 42;
bool interfaceWriteRejected = false;
try
{
    valuesOnly.Interfaces[0] = aReference;
}
catch (InvalidCastException)
{
    interfaceWriteRejected = true;
}
Check("Variant mode rejects interface assignment without changing data",
    interfaceWriteRejected &&
    valuesOnly.ObjectsMode == TJclStringListObjectsMode.omVariants &&
    (double)valuesOnly.Variants[0] == 42);

valuesOnly.Clear();
valuesOnly.Add("fresh");
Check("Clear retains Variant mode and resets associated values",
    valuesOnly.ObjectsMode == TJclStringListObjectsMode.omVariants &&
    VarIsEmpty(valuesOnly.Variants[0]));

IJclStringList parent = new TJclStringList();
parent.Add("child");
IJclStringList child = parent.Lists[0];
Check("Empty Lists cell creates and stores a child",
    child.Count == 0 &&
    ReferenceEquals(child, parent.Interfaces[0]) &&
    parent.ObjectsMode == TJclStringListObjectsMode.omInterfaces);

child.Add("inside");
child.Variants[0] = 7;
Check("Repeated Lists access returns the same child",
    ReferenceEquals(child, parent.Lists[0]) &&
    parent.Lists[0][0] == "inside" &&
    (double)parent.Lists[0].Variants[0] == 7);

IJclStringList replacement = new TJclStringList();
replacement.Add("replacement");
parent.Lists[0] = replacement;
Check("Lists assignment replaces the stored child reference",
    ReferenceEquals(parent.Interfaces[0], replacement) &&
    ReferenceEquals(parent.Lists[0], replacement));

parent.Interfaces[0] = aReference;
bool wrongChildRejected = false;
try
{
    IJclStringList incompatible = parent.Lists[0];
}
catch (InvalidCastException)
{
    wrongChildRejected = true;
}
Check("Lists rejects a non-list reference without replacing it",
    wrongChildRejected &&
    ReferenceEquals(parent.Interfaces[0], aReference));

parent.Interfaces[0] = null;
IJclStringList recreated = parent.Lists[0];
Check("Null Lists cell creates a fresh child on next access",
    !ReferenceEquals(recreated, child) &&
    !ReferenceEquals(recreated, replacement) &&
    recreated.Count == 0);

parent.Clear();
Check("Clearing parent does not clear an externally retained child",
    child.Count == 1 && child[0] == "inside" &&
    replacement.Count == 1 && replacement[0] == "replacement");

IJclStringList strictDuplicates = new TJclStringList();
strictDuplicates.Add("key");
strictDuplicates.Variants[0] = 17;
strictDuplicates.Sorted = true;
strictDuplicates.Duplicates = TDuplicates.dupError;
bool duplicateRejected = false;
try
{
    strictDuplicates.Add("KEY");
}
catch (ArgumentException)
{
    duplicateRejected = true;
}
Check("JCL dupError rejects a duplicate without changing associated data",
    duplicateRejected && strictDuplicates.Count == 1 &&
    strictDuplicates[0] == "key" &&
    (double)strictDuplicates.Variants[0] == 17);
Check("JCL dupError still permits a distinct sorted entry",
    strictDuplicates.Add("later") == 1 && strictDuplicates.Count == 2);
strictDuplicates.Duplicates = TDuplicates.dupAccept;
strictDuplicates.Add("key");
Check("JCL duplicate policy can change after a rejected insertion",
    strictDuplicates.Count == 3);

Console.WriteLine($"JCL core checks: {passed}/{passed + failed} passed.");
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