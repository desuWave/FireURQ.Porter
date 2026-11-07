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
unit AbstractTest;
interface
type
TBase = class
public
  procedure Store(const value: Integer); virtual; abstract;
  function ReadValue: Integer; virtual; abstract;
end;
TMiddle = class(TBase)
protected
  Saved: Integer;
public
  procedure Store(const value: Integer); override;
end;
TChild = class(TMiddle)
public
  function ReadValue: Integer; override;
end;
implementation
procedure TMiddle.Store(const value: Integer);
begin
  Saved := value;
end;
function TChild.ReadValue: Integer;
begin
  Result := Saved;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.AbstractCompile." + Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "AbstractTest.pas");
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
Console.WriteLine("=== Generated abstract contracts ===");
foreach (string line in generated.Split('\n').Where(line =>
    line.Contains("abstract") || line.Contains("override")))
    Console.WriteLine(line.Trim());

Check("Base class is abstract",
    generated.Contains("public abstract partial class TBase"));
Check("Intermediate class retains the remaining abstract contract",
    generated.Contains("public abstract partial class TMiddle : TBase"));
Check("Concrete child remains concrete",
    generated.Contains("public partial class TChild : TMiddle"));
Check("Abstract procedure uses void and in parameter",
    generated.Contains("public abstract void Store(in int value);"));
Check("Abstract function has return type",
    generated.Contains("public abstract int ReadValue();"));
Check("Procedure override matches contract",
    generated.Contains("public override void Store(in int value)"));
Check("Function override matches contract",
    generated.Contains("public override int ReadValue()"));
Check("Only two implementation bodies emitted",
    results[0].FunctionsTranslated == 2);

File.WriteAllText(Path.Combine(directory, "Abstract.csproj"), """
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
        TBase instance = new TChild();
        int value = 42;
        instance.Store(in value);
        bool passed = instance.ReadValue() == 42 &&
            typeof(TBase).IsAbstract &&
            typeof(TMiddle).IsAbstract &&
            !typeof(TChild).IsAbstract;
        System.Console.WriteLine(passed
            ? "[PASS] Abstract contract dispatch and stored value"
            : "[FAIL] Abstract contract behavior");
        return passed ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Abstract.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated abstract project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated abstract project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Abstract-contract checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;