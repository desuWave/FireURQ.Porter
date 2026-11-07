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
        foreach (var child in Walk(statement.Children)) yield return child;
        if (statement.ElseChildren is not null)
            foreach (var child in Walk(statement.ElseChildren)) yield return child;
    }
}

string source = """
unit XmlBoundaries;
interface
type
TChars = packed record a, b, c, d: Char end;
TOctet = packed record a, b, c: Byte; end;
TAfter = Integer;
implementation
function Choose(fail: Boolean): Integer;
begin
  if fail then
    raise Exception.Create('bad')
  else
    Result := 42;
end;
end.
""";
var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var chars = model.Types.Single(value => value.Name == "TChars");
Check("Four record fields preserve Char type",
    chars.Fields?.Count == 4 &&
    chars.Fields.All(field => field.Type.Equals("Char",
        StringComparison.OrdinalIgnoreCase)));
Check("Following record remains separate",
    model.Types.Single(value => value.Name == "TOctet").Fields?.Count == 3);
Check("Following alias survives record boundary",
    model.Types.Any(value => value.Name == "TAfter" && value.Kind == "Simple"));

var body = BodyTokenAnalyzer.Analyze(unit, "XmlBoundaries.pas")
    .Single(value => value.BodyTokens.Count > 0);
var behavior = BehaviorAnalyzer.Analyze(
    body.FilePath, body.Name, body.BodyTokens);
var conditional = Walk(behavior.Statements).Single(value =>
    value.Kind == BehaviorAnalyzer.StatementKind.If);
Check("Else branch survives raise",
    conditional.ElseChildren?.Count > 0);
Check("Raise expression excludes else",
    Walk(behavior.Statements)
        .Where(value => value.Kind == BehaviorAnalyzer.StatementKind.Raise)
        .All(value => !value.Text.Contains("else",
            StringComparison.OrdinalIgnoreCase)));

string compileDirectory = Path.Combine(
    Path.GetTempPath(), "FireURQ.XmlBoundaryCompile." +
    Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(compileDirectory, "Input");
Directory.CreateDirectory(inputRoot);
string inputPath = Path.Combine(inputRoot, "XmlBoundaries.pas");
File.WriteAllText(inputPath, source);
var input = new ProjectTranslationInput(
    inputRoot, inputPath, unit, model,
    new[] { (Body: body, Behavior: behavior) });
var results = new ProjectTranslator().Translate(
    new[] { input }, Path.Combine(compileDirectory, "Generated"));
Check("Project generation succeeds",
    results.Count == 1 && results[0].Success);
if (results.Count == 1 && results[0].Success)
{
    File.WriteAllText(Path.Combine(compileDirectory, "Xml.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
    File.WriteAllText(Path.Combine(compileDirectory, "Program.cs"), """
public static class Program
{
    public static int Main()
    {
        bool passed = XmlBoundariesModule.Choose(false) == 42;
        bool caught = false;
        try
        {
            XmlBoundariesModule.Choose(true);
        }
        catch (System.Exception exception)
        {
            caught = exception.Message == "bad";
        }
        passed &= caught;
        System.Console.WriteLine(passed
            ? "[PASS] Else result and exception branch"
            : "[FAIL] Branch behavior");
        return passed ? 0 : 1;
    }
}
""");
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("run");
    start.ArgumentList.Add("--project");
    start.ArgumentList.Add(Path.Combine(compileDirectory, "Xml.csproj"));
    start.ArgumentList.Add("--configuration");
    start.ArgumentList.Add("Release");
    using var process = Process.Start(start);
    if (process is null)
        Check("Generated project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated project compiles and executes", process.ExitCode == 0);
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\SimpleXML.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real SimpleXML preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var real = new DelphiParser(tokens).ParseUnit();
    var realModel = SemanticModelBuilder.Build(real);
    var record = realModel.Types.Single(value => value.Name == "TChars");
    Check("Real TChars has four clean Char fields",
        record.Fields?.Count == 4 &&
        record.Fields.All(field => field.Type.Equals(
            "Char", StringComparison.OrdinalIgnoreCase)));

    var realBody = BodyTokenAnalyzer.Analyze(real, path).Single(value =>
        value.Name == "TXmlSource.ExpectQuotedText" &&
        value.BodyTokens.Count > 0);
    var realBehavior = BehaviorAnalyzer.Analyze(
        path, realBody.Name, realBody.BodyTokens);
    var branches = Walk(realBehavior.Statements).ToArray();
    Check("Real error expression does not absorb else",
        branches.Where(value =>
            value.Kind == BehaviorAnalyzer.StatementKind.Raise)
        .All(value => !value.Text.Contains(
            "else", StringComparison.OrdinalIgnoreCase)));
    Check("Real quote parser retains an else branch",
        branches.Any(value =>
            value.Kind == BehaviorAnalyzer.StatementKind.If &&
            value.ElseChildren?.Count > 0));
}
Console.WriteLine($"XML-boundary checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + compileDirectory);
return failed == 0 ? 0 : 1;