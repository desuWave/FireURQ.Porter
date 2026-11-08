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
    if (!condition) failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

string directory = Path.Combine(
    Path.GetTempPath(), $"FireURQ.ProjectStringList.{Guid.NewGuid():N}");
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

var input = BuildInput("StringListProject", """
unit StringListProject;
interface
type
  TManualOwner = class(TStringList)
  private
    procedure ClearObjects;
  public
    ClearCalls: Integer;
    DeleteCalls: Integer;
    procedure Clear; override;
    procedure Delete(Index: Integer); override;
    destructor Destroy; override;
  end;
implementation

function MakeBare: tstringlist;
begin
  Result := TStringList.Create;
end;

function MakeExplicit: TStringList;
begin
  Result := TStringList.Create();
end;

procedure Configure(items: TStringList);
begin
  items.CaseSensitive := False;
  items.Sorted := True;
  items.Duplicates := dupError;
end;

function Attach(items: TStringList; name: string; value: TObject): Integer;
begin
  Result := items.AddObject(name, value);
end;

function ReadAttached(items: TStringList; index: Integer): TObject;
begin
  Result := items.Objects[index];
end;

procedure ClearPlain(items: TStringList);
begin
  items.Clear;
end;

procedure TManualOwner.ClearObjects;
var
  I: Integer;
begin
  for I := 0 to Count - 1 do
    if Objects[I] <> nil then
      Objects[I].Free;
end;

procedure TManualOwner.Clear;
begin
  Inc(ClearCalls);
  ClearObjects;
  inherited Clear;
end;

procedure TManualOwner.Delete(Index: Integer);
begin
  Inc(DeleteCalls);
  if Objects[Index] <> nil then
    Objects[Index].Free;
  inherited Delete(Index);
end;

destructor TManualOwner.Destroy;
begin
  ClearObjects;
  inherited Destroy;
end;

end.
""");

var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("String-list project translation succeeds",
    results.Count == 1 && results.All(result => result.Success));
Check("String-list runtime is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "StringLists.cs")));
Check("Shared duplicate policy is emitted automatically",
    File.Exists(Path.Combine(outputRoot, "__runtime", "StringListCommon.cs")));
Check("String-list project does not require JCL support",
    !File.Exists(Path.Combine(outputRoot, "__runtime", "JclStringLists.cs")));

string generated = File.ReadAllText(
    Path.Combine(outputRoot, "Input", "StringListProject.cs"));
Check("Duplicate constants are imported automatically",
    generated.Contains("using static global::FireURQ.Runtime.TDuplicates;"));
Check("Derived class retains its runtime base",
    generated.Contains("class TManualOwner : TStringList"));
Check("Derived Clear and Delete remain overrides",
    generated.Contains("public override void Clear(") &&
    generated.Contains("public override void Delete("));
Check("Derived destructor retains its lifetime scope",
    generated.Contains("public override void Destroy(") &&
    generated.Contains("__FireUrqEnterDestructor()"));

var plain = BuildInput("PlainStringListProbe", """
unit PlainStringListProbe;
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
Check("Plain project does not receive string-list dependencies",
    plainResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "StringLists.cs")) &&
    !File.Exists(Path.Combine(plainOutput, "__runtime", "StringListCommon.cs")));

var shadow = BuildInput("ShadowStringListProbe", """
unit ShadowStringListProbe;
interface
type
  TStringList = class
  end;
implementation
end.
""");
string shadowOutput = Path.Combine(directory, "ShadowGenerated");
var shadowResults = new ProjectTranslator().Translate(new[] { shadow }, shadowOutput);
Check("Project-defined TStringList does not enable standard support",
    shadowResults.All(result => result.Success) &&
    !File.Exists(Path.Combine(shadowOutput, "__runtime", "StringLists.cs")));

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
    if (condition) passed++; else failed++;
}

TStringList scenes = StringListProjectModule.MakeBare();
TStringList second = StringListProjectModule.MakeExplicit();
Check("Project-generated factories return independent lists",
    scenes.Count == 0 && second.Count == 0 &&
    !ReferenceEquals(scenes, second));

StringListProjectModule.Configure(scenes);
var alpha = new Tracked();
var beta = new Tracked();
StringListProjectModule.Attach(scenes, "beta", beta);
int index = StringListProjectModule.Attach(scenes, "alpha", alpha);
Check("Project-generated sorted insertion preserves object associations",
    index == 0 &&
    ReferenceEquals(StringListProjectModule.ReadAttached(scenes, 0), alpha));

bool rejected = false;
try
{
    StringListProjectModule.Attach(scenes, "ALPHA", beta);
}
catch (ArgumentException)
{
    rejected = true;
}
Check("Project-generated dupError rejects a duplicate without mutation",
    rejected && scenes.Count == 2 &&
    ReferenceEquals(StringListProjectModule.ReadAttached(scenes, 0), alpha));
StringListProjectModule.ClearPlain(scenes);
Check("Project-generated base Clear preserves borrowed objects",
    alpha.Destroys == 0 && beta.Destroys == 0);
TObject.Free(scenes);
TObject.Free(second);
TObject.Free(alpha);
TObject.Free(beta);

var owner = new TManualOwner();
var first = new Tracked();
var remaining = new Tracked();
owner.AddObject("first", first);
owner.AddObject("remaining", remaining);
TStrings baseView = owner;
baseView.Delete(0);
Check("Translated descendant Delete dispatches through TStrings",
    owner.DeleteCalls == 1 &&
    first.Destroys == 1 && first.Completions == 1 &&
    remaining.Destroys == 0);
baseView.Clear();
Check("Translated descendant Clear frees its remaining object once",
    owner.ClearCalls == 1 && owner.DeleteCalls == 1 &&
    remaining.Destroys == 1 && remaining.Completions == 1);

var last = new Tracked();
owner.AddObject("last", last);
TObject.Free(owner);
Check("Translated descendant destruction does not repeat cleanup",
    owner.ClearCalls == 1 && owner.DeleteCalls == 1 &&
    last.Destroys == 1 && last.Completions == 1 &&
    first.Destroys == 1 && remaining.Destroys == 1);

Console.WriteLine($"Project string-list execution: {passed}/{passed + failed} passed.");
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
        Check("Translated string-list project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Translated string-list project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Project-string-list checks: {total - failed}/{total} passed.");
Console.WriteLine($"Generated compilation directory: {directory}");
Environment.ExitCode = failed == 0 ? 0 : 1;