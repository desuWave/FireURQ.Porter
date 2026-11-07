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

var unit = new DelphiParser(new DelphiLexer("""
unit InheritedTest;
interface
type
TChild = class
  procedure Run(value: Integer);
  procedure Change(var value: Integer);
  procedure Release;
end;
implementation
procedure TChild.Run(value: Integer);
begin
  inherited Run(value + 1);
end;
procedure TChild.Change(var value: Integer);
begin
  inherited;
end;
procedure TChild.Release;
begin
  inherited Release;
end;
end.
""").Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var generated = new Dictionary<string, string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "InheritedTest.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null) continue;

    string text = translator.TranslateFunction(function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
    generated[function.Name] = text;
    Console.WriteLine(text);
}

Check("Explicit inherited call preserves argument",
    generated.TryGetValue("Run", out var run) &&
    run.Contains("base.Run(") && run.Contains("value + 1"));
Check("Bare inherited forwards var parameter",
    generated.TryGetValue("Change", out var change) &&
    change.Contains("base.Change(ref value);"));
Check("Named parameterless inherited becomes a call",
    generated.TryGetValue("Release", out var release) &&
    release.Contains("base.Release();"));

if (generated.Count == 3)
{
    string directory = Path.Combine(
        Path.GetTempPath(), "FireURQ.InheritedCompile." +
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "Inherited.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
    File.WriteAllText(Path.Combine(directory, "Program.cs"), """
public class Parent
{
    public int Seen;
    public int Releases;
    public void Run(int value) { Seen = value; }
    public void Change(ref int value) { value += 7; }
    public void Release() { Releases++; }
}
public class Child : Parent
{
""" + "\n" + string.Join("\n",
        generated.Values.Select(text => "public new " + text)) + "\n}\n" +
        """
public static class Program
{
    public static int Main()
    {
        var child = new Child();
        child.Run(4);
        int value = 3;
        child.Change(ref value);
        child.Release();
        bool passed = child.Seen == 5 && value == 10 && child.Releases == 1;
        System.Console.WriteLine(passed
            ? "[PASS] Base calls execute with expected arguments"
            : "[FAIL] Base call behavior");
        return passed ? 0 : 1;
    }
}
""");
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("run");
    start.ArgumentList.Add("--project");
    start.ArgumentList.Add(Path.Combine(directory, "Inherited.csproj"));
    start.ArgumentList.Add("--configuration");
    start.ArgumentList.Add("Release");
    using var process = Process.Start(start);
    if (process is null)
        Check("Generated inherited calls compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated inherited calls compile and execute",
            process.ExitCode == 0);
    }
    Console.WriteLine("Generated compilation directory: " + directory);
}
else
{
    Check("Generated inherited calls compile and execute", false);
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dClasses.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real d2dClasses preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var realUnit = new DelphiParser(tokens).ParseUnit();
    var realModel = SemanticModelBuilder.Build(realUnit);
    var realTranslator = new DelphiToCSharpTranslator(
        new TypeResolver(new[] { realModel }), realModel.Name);

    foreach (var entry in new[]
    {
        (Name: "Td2dProtoObjectPrim.Destroy", Expected: "base.Destroy();"),
        (Name: "Td2dProtoObjectPrim.FreeInstance", Expected: "base.FreeInstance();"),
        (Name: "Td2dProtoObject.Destroy", Expected: "base.Destroy();")
    })
    {
        var body = BodyTokenAnalyzer.Analyze(realUnit, path).Single(value =>
            value.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase) &&
            value.BodyTokens.Count > 0);
        var function = FunctionImplementationMatcher.Match(
            realUnit, body, realModel);
        string text = function is null ? "" :
            realTranslator.TranslateFunction(function,
                BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens));
        Check(entry.Name + ": base call retained", text.Contains(entry.Expected));
    }
}

Console.WriteLine($"Inherited-statement checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;