using System.Diagnostics;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

string source = """
unit RefCountTest;
interface
type
TCounted = class(TInterfacedObject)
public
  Destructions: Integer;
  Completions: Integer;
  function Acquire: Integer;
  function ReleaseReference: Integer;
  destructor Destroy; override;
protected
  procedure FreeInstance; override;
end;
TChild = class(TCounted)
end;
implementation
function TCounted.Acquire: Integer;
begin
  Result := _AddRef();
end;
function TCounted.ReleaseReference: Integer;
begin
  Result := _Release();
end;
destructor TCounted.Destroy;
begin
  Destructions := Destructions + 1;
  inherited;
end;
procedure TCounted.FreeInstance;
begin
  Completions := Completions + 1;
  inherited;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.InterfacedObject." +
    Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "RefCountTest.pas");
File.WriteAllText(path, source);

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
Check("Standard interfaced object inherits TObject.Free",
    resolver.IsInheritedTObjectFree("TInterfacedObject", model.Name));
Check("Generated descendant inherits TObject.Free",
    resolver.IsInheritedTObjectFree("TChild", model.Name));

var bodies = BodyTokenAnalyzer.Analyze(unit, path)
    .Where(body => body.BodyTokens.Count > 0)
    .Select(body => (
        Body: body,
        Behavior: BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens)))
    .ToList();
var input = new ProjectTranslationInput(inputRoot, path, unit, model, bodies);
var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("Project translation succeeds",
    results.Count == 1 && results[0].Success);
if (results.Count != 1 || !results[0].Success)
{
    foreach (var result in results) Console.WriteLine(result.Error);
    return 1;
}

string generated = File.ReadAllText(results[0].OutputFile);
Check("All four implementation bodies emitted",
    results[0].FunctionsTranslated == 4);
Check("Destructor override and lifetime scope emitted",
    generated.Contains("public override void Destroy()") &&
    generated.Contains("__FireUrqEnterDestructor()"));
Check("Reference operations remain calls",
    generated.Contains("_AddRef()") && generated.Contains("_Release()"));

string project = Path.Combine(directory, "Probe.csproj");
File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"), """
public static class Program
{
    public static int Main()
    {
        int failed = 0;
        void Check(string name, bool passed)
        {
            if (!passed) failed++;
            System.Console.WriteLine(
                $"[{(passed ? "PASS" : "FAIL")}] {name}");
        }

        var instance = new TChild();
        Check("Initial completed-construction reference count is zero",
            instance.RefCount == 0);
        Check("First acquisition returns one", instance.Acquire() == 1);
        Check("Second acquisition returns two", instance.Acquire() == 2);
        Check("First release returns one without destruction",
            instance.ReleaseReference() == 1 &&
            instance.RefCount == 1 &&
            instance.Destructions == 0 && instance.Completions == 0);
        Check("Last release destroys and completes exactly once",
            instance.ReleaseReference() == 0 &&
            instance.RefCount == 0 &&
            instance.Destructions == 1 && instance.Completions == 1);

        var shared = new TChild();
        shared.Acquire();
        System.Threading.Tasks.Parallel.For(0, 10000, index =>
        {
            shared.Acquire();
            shared.ReleaseReference();
        });
        Check("Parallel reference operations preserve the retained owner",
            shared.RefCount == 1 &&
            shared.Destructions == 0 && shared.Completions == 0);
        shared.ReleaseReference();
        Check("Final retained owner releases the object once",
            shared.RefCount == 0 &&
            shared.Destructions == 1 && shared.Completions == 1);

        return failed == 0 ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(project);
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated reference-count project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated reference-count project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Interfaced-object checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;