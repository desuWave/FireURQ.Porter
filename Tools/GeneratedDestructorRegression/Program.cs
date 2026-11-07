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
unit DestructorTest;
interface
type
TBase = class
public
  Stage: Integer;
  ReleaseStage: Integer;
  Destructions: Integer;
  Releases: Integer;
  destructor Destroy; override;
protected
  procedure FreeInstance; override;
end;
TChild = class(TBase)
public
  destructor Destroy; override;
end;
TCounted = class
public
  References: Integer;
  Cleanups: Integer;
  Releases: Integer;
  destructor Destroy; override;
protected
  procedure FreeInstance; override;
end;
implementation
destructor TBase.Destroy;
begin
  Stage := 2;
  Destructions := Destructions + 1;
  inherited;
end;
procedure TBase.FreeInstance;
begin
  ReleaseStage := Stage;
  Releases := Releases + 1;
  inherited;
end;
destructor TChild.Destroy;
begin
  Stage := 1;
  inherited;
  Stage := 3;
end;
destructor TCounted.Destroy;
begin
  References := References - 1;
  if References = 0 then
  begin
    References := References + 1;
    try
      Cleanups := Cleanups + 1;
      inherited;
    finally
      References := References - 1;
    end;
  end;
end;
procedure TCounted.FreeInstance;
begin
  if References = 0 then
  begin
    Releases := Releases + 1;
    inherited;
  end;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.GeneratedDestructor." +
    Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "DestructorTest.pas");
File.WriteAllText(path, source);

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
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
Check("Runtime support file emitted",
    File.Exists(Path.Combine(outputRoot, "__runtime", "TObject.cs")));
Check("Implicit TObject base emitted",
    generated.Contains("class TBase : global::FireURQ.Runtime.TObject") &&
    generated.Contains("class TCounted : global::FireURQ.Runtime.TObject"));
Check("All five implementation bodies emitted",
    results[0].FunctionsTranslated == 5);
Check("All three destructors have lifetime scopes",
    generated.Split("using var __fireurqDestructorScope =").Length - 1 == 3);
Check("Inherited destructor call retained",
    generated.Contains("base.Destroy();"));
Check("FreeInstance override remains protected",
    generated.Contains("protected override void FreeInstance()"));

Console.WriteLine("=== Generated destructor scope lines ===");
foreach (string line in generated.Split('\n').Where(line =>
    line.Contains("Destroy(") ||
    line.Contains("__fireurqDestructorScope") ||
    line.Contains("FreeInstance(")))
    Console.WriteLine(line.Trim());

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
using FireURQ.Runtime;
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

        TObject.Free(null);
        var child = new TChild();
        TObject baseReference = child;
        baseReference.Free();
        Check("Free dispatches to generated child destructor",
            child.Stage == 3 && child.Destructions == 1);
        Check("Storage completion follows the outer destructor",
            child.ReleaseStage == 3 && child.Releases == 1);

        var direct = new TChild();
        direct.Destroy();
        Check("Direct generated destructor completes once",
            direct.Destructions == 1 &&
            direct.Releases == 1 && direct.ReleaseStage == 3);

        var counted = new TCounted();
        counted.References = 2;
        TObject countedReference = counted;
        countedReference.Free();
        Check("First release retains the object",
            counted.References == 1 &&
            counted.Cleanups == 0 && counted.Releases == 0);
        countedReference.Free();
        Check("Last release cleans and completes once",
            counted.References == 0 &&
            counted.Cleanups == 1 && counted.Releases == 1);

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
        Check("Generated destructor project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated destructor project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Generated-destructor checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;