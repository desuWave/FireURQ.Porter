using System.Diagnostics;
using System.Text;
using FireURQ.Porter.AST;
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

IEnumerable<BehaviorAnalyzer.BehaviorStatement> Walk(
    IEnumerable<BehaviorAnalyzer.BehaviorStatement> statements)
{
    foreach (var statement in statements)
    {
        yield return statement;
        foreach (var child in Walk(statement.Children))
            yield return child;
        if (statement.ElseChildren is not null)
            foreach (var child in Walk(statement.ElseChildren))
                yield return child;
    }
}

string source = """
unit NestedCaseTest;
interface
implementation
function Choose(outerValue, innerValue: Integer; enabled: Boolean): Integer;
begin
  Result := -1;
  case outerValue of
    0:
      if enabled then
      begin
        case innerValue of
          1: Result := 10;
          2: Result := 20;
        else
          Result := 30;
        end;
      end
      else
        Result := 40;
    1: Result := 50;
  else
    Result := 60;
  end;
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var body = BodyTokenAnalyzer.Analyze(unit, "NestedCaseTest.pas")
    .Single(value => value.BodyTokens.Count > 0);
var behavior = BehaviorAnalyzer.Analyze(
    body.FilePath, body.Name, body.BodyTokens);
var nodes = Walk(behavior.Statements).ToArray();
var cases = nodes.Where(value =>
    value.Kind == BehaviorAnalyzer.StatementKind.Case).ToArray();

Check("Both case statements remain separate", cases.Length == 2);
Check("Outer case has exactly three branches",
    cases.Length > 0 && cases[0].Children.Count == 3);
Check("Outer first branch contains an if",
    cases.Length > 0 &&
    cases[0].Children[0].Children.Any(value =>
        value.Kind == BehaviorAnalyzer.StatementKind.If));
Check("Control statement does not become a case label",
    nodes.Where(value => value.CaseLabels is not null)
        .SelectMany(value => value.CaseLabels!)
        .All(label => !label.Contains("if ", StringComparison.OrdinalIgnoreCase)));

var function = FunctionImplementationMatcher.Match(unit, body, model);
Check("Function signature matches body", function is not null);
if (function is null)
    return 1;

string generated = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name)
    .TranslateFunction(function, behavior);
Console.WriteLine("=== Generated nested case ===");
Console.WriteLine(generated);

string compileDirectory = Path.Combine(
    Path.GetTempPath(), "FireURQ.NestedCaseCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(compileDirectory);
File.WriteAllText(Path.Combine(compileDirectory, "Cases.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(compileDirectory, "Program.cs"),
    "public static class Generated {\npublic static " + generated + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        bool passed =
            Generated.Choose(0, 1, true) == 10 &&
            Generated.Choose(0, 2, true) == 20 &&
            Generated.Choose(0, 9, true) == 30 &&
            Generated.Choose(0, 1, false) == 40 &&
            Generated.Choose(1, 1, true) == 50 &&
            Generated.Choose(9, 1, true) == 60;
        System.Console.WriteLine(passed
            ? "[PASS] All six branch outcomes"
            : "[FAIL] Nested case behavior");
        return passed ? 0 : 1;
    }
}
""");
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(compileDirectory, "Cases.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated nested case compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated nested case compiles and executes", process.ExitCode == 0);
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2ScnMain.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real scene preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var realUnit = new DelphiParser(tokens).ParseUnit();
    var realBody = BodyTokenAnalyzer.Analyze(realUnit, path).Single(value =>
        value.Name.Equals("Tf2MainScene.CheckWaitState",
            StringComparison.OrdinalIgnoreCase) &&
        value.BodyTokens.Count > 0);
    var realBehavior = BehaviorAnalyzer.Analyze(
        path, realBody.Name, realBody.BodyTokens);
    var realCases = Walk(realBehavior.Statements)
        .Where(value => value.Kind == BehaviorAnalyzer.StatementKind.Case)
        .ToArray();

    Check("Real CheckWaitState preserves both cases", realCases.Length == 2);
    Check("Real outer case has idle, more and default",
        realCases.Length > 0 && realCases[0].Children.Count == 3);
    Check("Real labels contain no if statement",
        Walk(realBehavior.Statements)
            .Where(value => value.CaseLabels is not null)
            .SelectMany(value => value.CaseLabels!)
            .All(label => !label.Contains(
                "if ", StringComparison.OrdinalIgnoreCase)));
}

Console.WriteLine($"Nested-case checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + compileDirectory);
return failed == 0 ? 0 : 1;