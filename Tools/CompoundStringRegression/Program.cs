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

ExpressionParser.ExpressionNode ParseExpression(string source) =>
    ExpressionParser.Parse(new DelphiLexer(source).Tokenize()
        .Where(token => token.Kind != TokenKind.EndOfFile).ToArray());

foreach (string expression in new[]
{
    "'Sample rate : %d'#13#10",
    "#13#10'Sound Device'",
    "'A'#9'B'",
    "'It''s'#13#10",
    "''"
})
{
    Check("Literal node: " + expression,
        ParseExpression(expression).Kind == ExpressionParser.ExpressionKind.Literal);
}

Check("Plus remains a binary operator",
    ParseExpression("'A' + 'B'").Kind == ExpressionParser.ExpressionKind.Binary);

var call = ParseExpression("Log('A'#13#10, 7)");
Check("Call keeps target and two arguments",
    call.Kind == ExpressionParser.ExpressionKind.Call &&
    call.Children.Count == 3 &&
    call.Children[1].Kind == ExpressionParser.ExpressionKind.Literal);

string source = """
unit CompoundStrings;
interface
implementation
function TextFirst: string;
begin
  Result := 'Sample rate : %d'#13#10;
end;
function CodeFirst: string;
begin
  Result := #13#10'Sound Device';
end;
function Mixed: string;
begin
  Result := 'A'#9'B';
end;
function Apostrophe: string;
begin
  Result := 'It''s'#13#10;
end;
function Empty: string;
begin
  Result := '';
end;
function Added: string;
begin
  Result := 'A' + 'B';
end;
end.
""";
var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var generated = new List<string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "CompoundStrings.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature found", function is not null);
    if (function is null) continue;
    string text = translator.TranslateFunction(function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
    generated.Add("public static " + text);
    Console.WriteLine(text);
}
Check("All six bodies generated", generated.Count == 6);

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.CompoundCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "Strings.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"),
    "public static class Generated {\n" +
    string.Join("\n", generated) + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        bool passed =
            Generated.TextFirst() == "Sample rate : %d\r\n" &&
            Generated.CodeFirst() == "\r\nSound Device" &&
            Generated.Mixed() == "A\tB" &&
            Generated.Apostrophe() == "It's\r\n" &&
            Generated.Empty() == "" &&
            Generated.Added() == "AB";
        System.Console.WriteLine(passed
            ? "[PASS] All six generated string values"
            : "[FAIL] Generated string content");
        return passed ? 0 : 1;
    }
}
""");
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Strings.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated strings compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated strings compile and execute", process.ExitCode == 0);
    }
}
Console.WriteLine($"Compound-string checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;