using System.Diagnostics;
using System.Text;
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
unit VariantHelpersTest;
interface
function EnsureReal(const aValue: Variant): Double;
function EnsureString(const aValue: Variant): string;
function SumValue(aLeft: Variant; aRight: Variant): Variant;
function EqualValue(aLeft: Variant; aRight: Variant): Variant;
implementation
function EnsureReal(const aValue: Variant): Double;
var
  l_Str: string;
begin
  if VarType(aValue) = varString then
  begin
    l_Str := aValue;
    Result := Length(l_Str);
  end
  else
    Result := aValue;
end;
function EnsureString(const aValue: Variant): string;
begin
  if VarType(aValue) <> varString then
    Result := ''
  else
    Result := aValue;
end;
function SumValue(aLeft: Variant; aRight: Variant): Variant;
begin
  if (VarType(aLeft) = varString) and (VarType(aRight) = varString) then
    Result := EnsureString(aLeft) + EnsureString(aRight)
  else
    Result := EnsureReal(aLeft) + EnsureReal(aRight);
end;
function EqualValue(aLeft: Variant; aRight: Variant): Variant;
begin
  if VarType(aLeft) = varString then
  begin
    if EnsureString(aLeft) = EnsureString(aRight) then
      Result := 1.0
    else
      Result := 0.0;
  end
  else
    if EnsureReal(aLeft) = EnsureReal(aRight) then
      Result := 1.0
    else
      Result := 0.0;
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var bodies = BodyTokenAnalyzer.Analyze(unit, "VariantHelpersTest.pas")
    .Where(body => body.BodyTokens.Count > 0).ToList();
Check("All four Pascal helper bodies found", bodies.Count == 4);

var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var generated = new StringBuilder();
generated.AppendLine("using FireURQ.Runtime;");
generated.AppendLine("using static FireURQ.Runtime.DelphiVariants;");
generated.AppendLine("public static class Urql");
generated.AppendLine("{");
// Framework bridge for Pascal Length; Variant helpers come from the runtime.
generated.AppendLine("private static int Length(string value) => value.Length;");
int translated = 0;
foreach (var body in bodies)
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    if (function is null) continue;
    var behavior = BehaviorAnalyzer.Analyze(
        "VariantHelpersTest.pas", body.Name, body.BodyTokens);
    generated.Append("public static ");
    generated.AppendLine(translator.TranslateFunction(function, behavior));
    translated++;
}
generated.AppendLine("}");
Check("All four helper signatures matched and translated", translated == 4);

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.GeneratedVariant." +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(
    Path.Combine(directory, "Variant.cs"), CSharpVariantRuntime.Source);
File.WriteAllText(
    Path.Combine(directory, "Urql.cs"), generated.ToString());
Console.WriteLine("=== Generated helpers ===");
Console.WriteLine(generated);

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

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("Generated EnsureReal uses string length", Urql.EnsureReal("123") == 3);
Check("Generated EnsureReal preserves numeric values", Urql.EnsureReal(12.5) == 12.5);
Check("Generated EnsureReal preserves Delphi Boolean conversion",
    Urql.EnsureReal(true) == -1);
Check("Generated EnsureReal accepts Unassigned as zero",
    Urql.EnsureReal(Variant.Unassigned) == 0);
Check("Generated EnsureString preserves strings",
    Urql.EnsureString("ривіт") == "ривіт");
Check("Generated EnsureString returns empty text for non-strings",
    Urql.EnsureString(123) == "" && Urql.EnsureString(Variant.Null) == "");

Variant concatenated = Urql.SumValue("12", "3");
Check("Generated string sum concatenates",
    concatenated.TryGetString(out string text) && text == "123");
Check("Generated mixed sum uses string length",
    (double)Urql.SumValue("123", 2) == 5);
Check("Generated equality preserves left-operand rules",
    (double)Urql.EqualValue("a", 1) == 0 &&
    (double)Urql.EqualValue(1, "a") == 1);

bool nullRejected = false;
try
{
    double unused = Urql.EnsureReal(Variant.Null);
}
catch (InvalidCastException)
{
    nullRejected = true;
}
Check("Generated numeric conversion rejects Null", nullRejected);

Console.WriteLine($"Generated helper execution: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
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
        Check("Generated Variant helpers compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated Variant helpers compile and execute", process.ExitCode == 0);
    }
}
Console.WriteLine($"Generated-Variant checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;