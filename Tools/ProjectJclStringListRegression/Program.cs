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
    Path.GetTempPath(), $"FireURQ.ProjectJcl.{Guid.NewGuid():N}");
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

var input = BuildInput("JclProject", """
unit JclProject;
interface
implementation

function MakeList: IJclStringList;
begin
  Result := JclStringList;
end;

procedure Configure(items: IJclStringList);
begin
  items.CaseSensitive := FaLsE;
  items.Sorted := TrUe;
  items.Duplicates := dupIgnore;
end;

function AddValue(items: IJclStringList; text: string; value: Variant): Integer;
begin
  Result := items.Add(text);
  items.Variants[Result] := value;
end;

function ReadValue(items: IJclStringList; index: Integer): Variant;
begin
  Result := items.Variants[index];
end;

function ReadChild(parent: IJclStringList; index: Integer): IJclStringList;
begin
  Result := parent.Lists[index];
end;

end.
""");

var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("JCL project translation succeeds",
    results.Count == 1 && results.All(result => result.Success));

string generatedPath = Path.Combine(outputRoot, "Input", "JclProject.cs");
string generated = File.Exists(generatedPath)
    ? File.ReadAllText(generatedPath)
    : string.Empty;

Check("JCL support is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "JclStringLists.cs")));
Check("JCL factory is imported automatically",
    generated.Contains(
        "using static global::FireURQ.Runtime.DelphiJclStringLists;"));
Check("Duplicate constants are imported automatically",
    generated.Contains(
        "using static global::FireURQ.Runtime.TDuplicates;"));
Check("Mixed-case Pascal False becomes a C# Boolean",
    generated.Contains("items.CaseSensitive = false;"));
Check("Mixed-case Pascal True becomes a C# Boolean",
    generated.Contains("items.Sorted = true;"));
Check("All five implementation bodies are emitted",
    generated.Contains("MakeList(") &&
    generated.Contains("Configure(") &&
    generated.Contains("AddValue(") &&
    generated.Contains("ReadValue(") &&
    generated.Contains("ReadChild("));

var plainInput = BuildInput("PlainJclProbe", """
unit PlainJclProbe;
interface
implementation
function Number: Integer;
begin
  Result := 7;
end;
end.
""");
string plainOutput = Path.Combine(directory, "PlainGenerated");
var plainResults = new ProjectTranslator().Translate(
    new[] { plainInput }, plainOutput);
string plainGenerated = File.ReadAllText(
    Path.Combine(plainOutput, "Input", "PlainJclProbe.cs"));

Check("Plain project translation succeeds",
    plainResults.Count == 1 && plainResults.All(result => result.Success));
Check("Plain project does not receive JCL support",
    !File.Exists(Path.Combine(plainOutput, "__runtime", "JclStringLists.cs")));
Check("Plain project does not import JCL helpers",
    !plainGenerated.Contains("DelphiJclStringLists") &&
    !plainGenerated.Contains("Runtime.TDuplicates"));

var shadowInput = BuildInput("ShadowJclProbe", """
unit ShadowJclProbe;
interface
type
  IJclStringList = class
  end;
implementation
end.
""");
string shadowOutput = Path.Combine(directory, "ShadowGenerated");
var shadowResults = new ProjectTranslator().Translate(
    new[] { shadowInput }, shadowOutput);
string shadowGenerated = File.ReadAllText(
    Path.Combine(shadowOutput, "Input", "ShadowJclProbe.cs"));

Check("Project-defined IJclStringList translates successfully",
    shadowResults.Count == 1 &&
    shadowResults.All(result => result.Success));
Check("Project-defined IJclStringList does not enable standard support",
    !File.Exists(Path.Combine(
        shadowOutput, "__runtime", "JclStringLists.cs")) &&
    !shadowGenerated.Contains("DelphiJclStringLists"));

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

IJclStringList items = JclProjectModule.MakeList();
JclProjectModule.Configure(items);
JclProjectModule.AddValue(items, "zeta", 90);
int alpha = JclProjectModule.AddValue(items, "alpha", 10);
Check("Project-generated sorted insertion returns the correct index",
    alpha == 0 && items[0] == "alpha" && items[1] == "zeta");
Check("Project-generated Variant access preserves associations",
    (double)JclProjectModule.ReadValue(items, 0) == 10 &&
    (double)JclProjectModule.ReadValue(items, 1) == 90);

int duplicate = items.Add("ALPHA");
Check("Project-generated duplicate configuration is effective",
    duplicate == 0 && items.Count == 2 &&
    (double)JclProjectModule.ReadValue(items, 0) == 10);

IJclStringList parent = JclProjectModule.MakeList();
parent.Add("child");
IJclStringList child = JclProjectModule.ReadChild(parent, 0);
Check("Project-generated nested-list access stores the created child",
    ReferenceEquals(child, parent.Interfaces[0]));
Check("Project-generated factory returns independent lists",
    !ReferenceEquals(parent, items) && !ReferenceEquals(child, parent));

Console.WriteLine($"Project JCL execution: {passed}/{passed + failed} passed.");
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
        "run", "--project", project,
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Translated JCL project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Translated JCL project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Project-JCL checks: {total - failed}/{total} passed.");
Console.WriteLine($"Generated compilation directory: {directory}");
Environment.ExitCode = failed == 0 ? 0 : 1;