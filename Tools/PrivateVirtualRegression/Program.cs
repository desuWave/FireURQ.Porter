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
unit DispatchTest;
interface
type
TBase = class
private
  function Hidden: Integer; virtual;
private
  function Secret: Integer;
public
  function Value: Integer; virtual;
  function ReadHidden: Integer;
  function ReadSecret: Integer;
end;
TChild = class(TBase)
private
  function Hidden: Integer; override;
public
  function Value: Integer; override;
end;
implementation
function TBase.Hidden: Integer;
begin
  Result := 10;
end;
function TBase.Secret: Integer;
begin
  Result := 7;
end;
function TBase.Value: Integer;
begin
  Result := 1;
end;
function TBase.ReadHidden: Integer;
begin
  Result := Hidden();
end;
function TBase.ReadSecret: Integer;
begin
  Result := Secret();
end;
function TChild.Hidden: Integer;
begin
  Result := 20;
end;
function TChild.Value: Integer;
begin
  Result := 2;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.DispatchCompile." + Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "DispatchTest.pas");
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
Console.WriteLine("=== Generated method signatures ===");
foreach (string line in generated.Split('\n').Where(line =>
    line.Contains(" int ") && line.Contains("(")))
    Console.WriteLine(line.Trim());

Check("Public virtual method emitted",
    generated.Contains("public virtual int Value("));
Check("Public override method emitted",
    generated.Contains("public override int Value("));
Check("Protected virtual method emitted",
    generated.Contains("protected virtual int Hidden("));
Check("Protected override method emitted",
    generated.Contains("protected override int Hidden("));
Check("Private method visibility preserved",
    generated.Contains("private int Secret("));
Check("All seven implementations emitted",
    results[0].FunctionsTranslated == 7);

File.WriteAllText(Path.Combine(directory, "Dispatch.csproj"), """
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
        TBase plain = new TBase();
        TBase child = new TChild();

        bool passed =
            plain.Value() == 1 &&
            child.Value() == 2 &&
            plain.ReadHidden() == 10 &&
            child.ReadHidden() == 20 &&
            plain.ReadSecret() == 7 &&
            child.ReadSecret() == 7;

        System.Console.WriteLine(passed
            ? "[PASS] Public and protected virtual dispatch; private method calls"
            : "[FAIL] Instance dispatch behavior");
        return passed ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Dispatch.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");

using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated project compiles and executes", process.ExitCode == 0);
    }
}

Console.WriteLine($"Instance-dispatch checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;