using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool condition)
{
    total++;
    if (!condition)
        failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

const string source = """
unit InterfaceListBinding;
interface
type
  IProbe = interface
    function GetValue: Integer;
  end;
  IChildProbe = interface(IProbe)
    function Extra: Integer;
  end;
implementation

function MakeList: iinterfacelist;
begin
  Result := TInterfaceList.cReAtE;
end;

function MakeExplicit: IInterfaceList;
begin
  Result := TInterfaceList.Create();
end;

function AddProbe(items: IInterfaceList; value: IProbe): Integer;
begin
  Result := items.Add(value);
end;

function ReadProbe(items: IInterfaceList; index: Integer): IProbe;
begin
  Result := items.Items[index] as IProbe;
end;

function ReadChild(items: IInterfaceList; index: Integer): IChildProbe;
begin
  Result := items[index] as IChildProbe;
end;

procedure ReplaceProbe(items: IInterfaceList; index: Integer; value: IProbe);
begin
  items.Items[index] := value;
end;

function RemoveProbe(items: IInterfaceList; value: IProbe): Integer;
begin
  Result := items.Remove(value);
end;

procedure ClearInterfaces(items: IInterfaceList);
begin
  items.Clear;
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
var translator = new DelphiToCSharpTranslator(resolver, model.Name);

string declarations = new CSharpDeclarationTranslator(
    resolver, model.Name).Translate(model);
Console.WriteLine("=== Generated interfaces ===");
Console.WriteLine(declarations);
Check("Implicit interface ancestor becomes IInterface",
    declarations.Contains("public interface IProbe : IInterface"));
Check("Explicit interface ancestor is preserved",
    declarations.Contains("public interface IChildProbe : IProbe"));

var methods = new Dictionary<string, string>();
foreach (var body in BodyTokenAnalyzer.Analyze(unit, "InterfaceListBinding.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null)
        continue;

    methods[function.Name] = translator.TranslateFunction(
        function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
}

Check("All eight Pascal interface-list helpers are translated",
    methods.Count == 8);
Check("Mixed-case interface-list return type is normalized",
    methods.TryGetValue("MakeList", out var makeList) &&
    makeList.Contains("IInterfaceList MakeList("));
Check("Bare and explicit factories remain single calls",
    methods.TryGetValue("MakeExplicit", out var makeExplicit) &&
    makeList is not null &&
    makeList.Contains("TInterfaceList.Create();") &&
    makeExplicit.Contains("TInterfaceList.Create();") &&
    !makeExplicit.Contains("Create()("));

Console.WriteLine("=== Generated interface-list helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

if (failed == 0)
{
    string directory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedInterfaceList.{Guid.NewGuid():N}");
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

    string helpers = "using System;\nusing FireURQ.Runtime;\n" +
        declarations + "\npublic static class Generated\n{\n" +
        string.Join("\n",
            methods.Values.Select(value => "public static " + value)) +
        "\n}\n";

    File.WriteAllText(Path.Combine(directory, "Generated.cs"),
        helpers, encoding);

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

IInterfaceList items = Generated.MakeList();
IInterfaceList otherList = Generated.MakeExplicit();
// C# assignments do not yet acquire Delphi interface ownership automatically.
items._AddRef();
otherList._AddRef();
Check("Both translated factories produce independent empty lists",
    items.Count == 0 && otherList.Count == 0 &&
    !ReferenceEquals(items, otherList));

var first = new Tracked(17);
((IInterface)first)._AddRef();
IProbe firstProbe = first;
int index = Generated.AddProbe(items, firstProbe);
Check("Generated Add accepts an implicitly inherited interface",
    index == 0 && first.RefCount == 2);

IProbe read = Generated.ReadProbe(items, index);
IChildProbe child = Generated.ReadChild(items, index);
Check("Generated Items and default-index casts preserve interface identity",
    ReferenceEquals(read, first) &&
    ReferenceEquals(child, first) &&
    read.GetValue() == 17 && child.Extra() == 23);
Check("Read helpers retain the current borrowed-reference contract",
    first.RefCount == 2);

var replacement = new Tracked(31);
((IInterface)replacement)._AddRef();
Generated.ReplaceProbe(items, index, replacement);
Check("Generated replacement transfers cell ownership",
    first.RefCount == 1 &&
    replacement.RefCount == 2 &&
    ReferenceEquals(Generated.ReadProbe(items, index), replacement));
Generated.ReplaceProbe(items, index, replacement);
Check("Generated self-assignment preserves the reference count",
    replacement.RefCount == 2);

Check("Generated Remove releases only the list-held reference",
    Generated.RemoveProbe(items, replacement) == 0 &&
    items.Count == 0 &&
    replacement.RefCount == 1 &&
    replacement.Destroys == 0);

Generated.AddProbe(items, first);
Generated.AddProbe(items, replacement);
Generated.ClearInterfaces(items);
Check("Generated Clear preserves explicitly retained external owners",
    items.Count == 0 &&
    first.RefCount == 1 && replacement.RefCount == 1 &&
    first.Destroys == 0 && replacement.Destroys == 0);

((IInterface)first)._Release();
((IInterface)replacement)._Release();
Check("External final releases destroy each value exactly once",
    first.Destroys == 1 && first.Completions == 1 &&
    replacement.Destroys == 1 && replacement.Completions == 1);

var listHeld = new Tracked(99);
Generated.AddProbe(otherList, listHeld);
Check("Final list release destroys a value held only by that list",
    otherList._Release() == 0 &&
    listHeld.Destroys == 1 && listHeld.Completions == 1);
items._Release();

Console.WriteLine($"Generated interface-list execution: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TInterfacedObject, IChildProbe
{
    private readonly int value;
    public int Destroys;
    public int Completions;

    public Tracked(int value) => this.value = value;
    public int GetValue() => value;
    public int Extra() => 23;

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
        Check("Generated interface-list helpers compile and execute", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated interface-list helpers compile and execute",
            process.ExitCode == 0);
    }
    Console.WriteLine($"Generated compilation directory: {directory}");
}

Console.WriteLine($"Generated-interface-list checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;