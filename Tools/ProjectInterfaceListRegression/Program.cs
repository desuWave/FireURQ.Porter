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

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.ProjectInterfaceLists.{Guid.NewGuid():N}");
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
var encoding = new UTF8Encoding(false);

ProjectTranslationInput BuildInput(string name, string source)
{
    string file = Path.Combine(inputRoot, name + ".pas");
    File.WriteAllText(file, source, encoding);
    var unit = new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit();
    var model = SemanticModelBuilder.Build(unit);
    var bodies = BodyTokenAnalyzer.Analyze(unit, file)
        .Where(body => body.BodyTokens.Count > 0)
        .Select(body => (
            Body: body,
            Behavior: BehaviorAnalyzer.Analyze(
                body.FilePath, body.Name, body.BodyTokens)))
        .ToList();
    return new ProjectTranslationInput(
        inputRoot, file, unit, model, bodies);
}

var input = BuildInput("InterfaceListProject", """
unit InterfaceListProject;
interface
implementation

function MakeList: IInterfaceList;
begin
  Result := TInterfaceList.Create;
end;

function MakeExplicit: iinterfacelist;
begin
  Result := TInterfaceList.Create();
end;

function AddValue(items: IInterfaceList; value: IInterface): Integer;
begin
  Result := items.Add(value);
end;

function ReadValue(items: IInterfaceList; index: Integer): IInterface;
begin
  Result := items.Items[index];
end;

procedure ReplaceValue(items: IInterfaceList; index: Integer; value: IInterface);
begin
  items[index] := value;
end;

procedure ClearItems(items: IInterfaceList);
begin
  items.Clear;
end;

end.
""");

var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("Interface-list project translation succeeds",
    results.Count == 1 && results.All(result => result.Success));
Check("Interface-list support is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "InterfaceLists.cs")));
Check("Pointer-list support is not required by interface-list support",
    !File.Exists(Path.Combine(outputRoot, "__runtime", "Lists.cs")));

string generated = File.ReadAllText(
    Path.Combine(outputRoot, "Input", "InterfaceListProject.cs"));
Check("Both interface-list factories become single calls",
    generated.Split("result = TInterfaceList.Create();").Length - 1 == 2 &&
    !generated.Contains("Create()("));
Check("Mixed-case interface-list type is normalized",
    generated.Contains("IInterfaceList MakeExplicit("));
Check("All six implementation bodies are emitted",
    new[]
    {
        "MakeList(", "MakeExplicit(", "AddValue(",
        "ReadValue(", "ReplaceValue(", "ClearItems("
    }.All(generated.Contains));

var plain = BuildInput("PlainInterfaceListProbe", """
unit PlainInterfaceListProbe;
interface
implementation
function Number: Integer;
begin
  Result := 1;
end;
end.
""");
string plainOutput = Path.Combine(directory, "PlainGenerated");
var plainResults = new ProjectTranslator().Translate(new[] { plain }, plainOutput);
Check("Plain project does not receive interface-list support",
    plainResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "InterfaceLists.cs")));

var shadow = BuildInput("ShadowInterfaceListProbe", """
unit ShadowInterfaceListProbe;
interface
type
  IInterfaceList = class
  end;
implementation
end.
""");
string shadowOutput = Path.Combine(directory, "ShadowGenerated");
var shadowResults = new ProjectTranslator().Translate(new[] { shadow }, shadowOutput);
Check("Project-defined IInterfaceList does not enable standard support",
    shadowResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(shadowOutput, "__runtime", "InterfaceLists.cs")));

if (failed == 0)
{
    string project = Path.Combine(directory, "Probe.csproj");
    File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Generated/**/*.cs" />
    <Compile Include="Probe.cs" />
  </ItemGroup>
</Project>
""", encoding);

    File.WriteAllText(Path.Combine(directory, "Probe.cs"), """
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

IInterfaceList list = InterfaceListProjectModule.MakeList();
IInterfaceList second = InterfaceListProjectModule.MakeExplicit();
// Caller ownership is explicit until interface-variable lifetime translation exists.
list._AddRef();
second._AddRef();

Check("Project-generated factories return independent empty lists",
    list.Count == 0 && second.Count == 0 &&
    !ReferenceEquals(list, second));

var first = new Tracked();
var replacement = new Tracked();
((IInterface)first)._AddRef();
((IInterface)replacement)._AddRef();

int index = InterfaceListProjectModule.AddValue(list, first);
Check("Project-generated Add retains the interface",
    index == 0 && first.RefCount == 2);
Check("Project-generated Items read preserves identity",
    ReferenceEquals(
        InterfaceListProjectModule.ReadValue(list, index), first));

InterfaceListProjectModule.ReplaceValue(list, index, replacement);
Check("Project-generated replacement transfers cell ownership",
    first.RefCount == 1 && replacement.RefCount == 2 &&
    ReferenceEquals(
        InterfaceListProjectModule.ReadValue(list, index), replacement));

InterfaceListProjectModule.ClearItems(list);
Check("Project-generated Clear releases only cell ownership",
    list.Count == 0 && replacement.RefCount == 1 &&
    first.Destroys == 0 && replacement.Destroys == 0);

((IInterface)first)._Release();
((IInterface)replacement)._Release();
Check("Explicit external final releases complete destruction once",
    first.Destroys == 1 && first.Completions == 1 &&
    replacement.Destroys == 1 && replacement.Completions == 1);

var held = new Tracked();
InterfaceListProjectModule.AddValue(second, held);
Check("Final interface-list release destroys its sole retained value",
    second._Release() == 0 &&
    held.Destroys == 1 && held.Completions == 1);
list._Release();

Console.WriteLine($"Project interface-list execution: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

sealed class Tracked : TInterfacedObject
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
        "run", "--project", project,
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Translated interface-list project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Translated interface-list project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Project-interface-list checks: {total - failed}/{total} passed.");
Console.WriteLine($"Generated compilation directory: {directory}");
Environment.ExitCode = failed == 0 ? 0 : 1;