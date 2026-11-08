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
    Path.GetTempPath(), $"FireURQ.ProjectLists.{Guid.NewGuid():N}");
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

var input = BuildInput("ListProject", """
unit ListProject;
interface
implementation

function MakePointersBare: Tlist;
begin
  Result := TList.cReAtE;
end;

function MakePointersExplicit: TList;
begin
  Result := TList.Create();
end;

function MakeOwned: tobjectlist;
begin
  Result := TObjectList.Create;
end;

function MakeBorrowed: TObjectList;
begin
  Result := TObjectList.Create(False);
end;

procedure AppendObject(items: TObjectList; value: TObject);
begin
  items.Add(value);
end;

procedure ClearList(items: TObjectList);
begin
  items.Clear;
end;

end.
""");

var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("List project translation succeeds",
    results.Count == 1 && results.All(result => result.Success));
Check("List runtime is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "Lists.cs")));

string generated = File.ReadAllText(
    Path.Combine(outputRoot, "Input", "ListProject.cs"));
Check("Mixed-case runtime types use canonical C# names",
    generated.Contains("TList MakePointersBare(") &&
    generated.Contains("TObjectList MakeOwned("));
Check("Both pointer factory spellings become calls",
    generated.Split("result = TList.Create();").Length - 1 == 2);
Check("Bare object factory becomes a call",
    generated.Contains("result = TObjectList.Create();"));
Check("Explicit ownership argument remains a single call",
    generated.Contains("result = TObjectList.Create(false);") &&
    !generated.Contains("Create()("));
Check("All six implementation bodies are emitted",
    new[]
    {
        "MakePointersBare(", "MakePointersExplicit(", "MakeOwned(",
        "MakeBorrowed(", "AppendObject(", "ClearList("
    }.All(generated.Contains));

var plain = BuildInput("PlainListProbe", """
unit PlainListProbe;
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
Check("Plain project translates without list support",
    plainResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "Lists.cs")));

var shadow = BuildInput("ShadowListProbe", """
unit ShadowListProbe;
interface
type
  TList = class
  end;
implementation
end.
""");
string shadowOutput = Path.Combine(directory, "ShadowGenerated");
var shadowResults = new ProjectTranslator().Translate(new[] { shadow }, shadowOutput);
Check("Project-defined TList does not enable standard list support",
    shadowResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(shadowOutput, "__runtime", "Lists.cs")));

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

TList first = ListProjectModule.MakePointersBare();
TList second = ListProjectModule.MakePointersExplicit();
Check("Both generated pointer factories create independent lists",
    first.Count == 0 && second.Count == 0 &&
    !ReferenceEquals(first, second));
first.Add(new IntPtr(-1));
Check("Project-generated pointer list preserves native values",
    first.Items[0] == new IntPtr(-1));

TObjectList owned = ListProjectModule.MakeOwned();
TObjectList borrowed = ListProjectModule.MakeBorrowed();
Check("Generated object factories preserve ownership",
    owned.OwnsObjects && !borrowed.OwnsObjects);

var value = new Tracked();
ListProjectModule.AppendObject(owned, value);
ListProjectModule.AppendObject(borrowed, value);
ListProjectModule.ClearList(borrowed);
Check("Generated non-owning Clear preserves the object",
    borrowed.Count == 0 && value.Destroys == 0);
ListProjectModule.ClearList(owned);
Check("Generated owning Clear destroys and completes once",
    owned.Count == 0 && value.Destroys == 1 && value.Completions == 1);

TObject.Free(first);
TObject.Free(second);
TObject.Free(owned);
TObject.Free(borrowed);
Check("Cleared-list destruction does not repeat object destruction",
    value.Destroys == 1 && value.Completions == 1);

Console.WriteLine($"Project list execution: {passed}/{passed + failed} passed.");
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
        "run", "--project", project,
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Translated list project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Translated list project compiles and executes", process.ExitCode == 0);
    }
}

Console.WriteLine($"Project-list checks: {total - failed}/{total} passed.");
Console.WriteLine($"Generated compilation directory: {directory}");
Environment.ExitCode = failed == 0 ? 0 : 1;