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

SemanticUnit Parse(string source) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit());

var ordinary = Parse("""
unit Ordinary;
interface
const
Angle = Pi / 180;
Circle = 2 * pI;
Text = 'Pi / 180';
implementation
end.
""");

var local = Parse("""
unit Local;
interface
const Pi = 3;
Angle = Pi / 180;
implementation
end.
""");

var imported = Parse(
    "unit Imported; interface const Pi = 3; implementation end.");
var consumer = Parse(
    "unit Consumer; interface uses Imported; " +
    "const Angle = Pi / 180; implementation end.");
var second = Parse(
    "unit Second; interface const Pi = 4; implementation end.");
var ambiguous = Parse(
    "unit Ambiguous; interface uses Imported, Second; " +
    "const Angle = Pi / 180; implementation end.");

var resolver = new TypeResolver(
    new[] { ordinary, local, imported, consumer, second, ambiguous });

string Emit(SemanticUnit unit) =>
    new CSharpDeclarationTranslator(resolver, unit.Name)
        .TranslateModuleMembers(unit);

string generated = Emit(ordinary);
Console.WriteLine("=== Generated Pi constants ===");
Console.WriteLine(generated);

Check("Pi division uses double and qualified Math.PI",
    generated.Contains(
        "public const double Angle = global::System.Math.PI / 180;"));
Check("Pi lookup ignores case",
    generated.Contains(
        "public const double Circle = 2 * global::System.Math.PI;"));
Check("String content remains unchanged",
    generated.Contains("public const string Text = \"Pi / 180\";"));
Check("Local Pi blocks built-in substitution",
    !Emit(local).Contains("System.Math.PI"));
Check("Imported Pi blocks built-in substitution",
    !Emit(consumer).Contains("System.Math.PI"));
Check("Conflicting imported Pi does not become built-in",
    !Emit(ambiguous).Contains("System.Math.PI"));
Check("Legacy translator handles built-in Pi",
    new CSharpDeclarationTranslator().TranslateModuleMembers(ordinary)
        .Contains("global::System.Math.PI / 180"));
Check("Legacy translator preserves local Pi",
    !new CSharpDeclarationTranslator().TranslateModuleMembers(local)
        .Contains("System.Math.PI"));

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.PiCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

File.WriteAllText(Path.Combine(directory, "PiCompile.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");

File.WriteAllText(Path.Combine(directory, "Program.cs"),
    "public static class Constants {\n" + generated + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        bool passed =
            Constants.Angle == System.Math.PI / 180 &&
            Constants.Circle == 2 * System.Math.PI &&
            Constants.Text == "Pi / 180";
        System.Console.WriteLine(passed
            ? "[PASS] Generated Pi values"
            : "[FAIL] Generated Pi values");
        return passed ? 0 : 1;
    }
}
""");

var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "PiCompile.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");

using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated constants compile and execute", false);
    else
    {
        process.WaitForExit();
        Check("Generated constants compile and execute", process.ExitCode == 0);
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path =
    @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2Decorators.pas";
var processor = new DelphiConditionalPreprocessor();
bool ok = processor.TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);

Check("Real decorator preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var model = SemanticModelBuilder.Build(
        new DelphiParser(tokens).ParseUnit());
    var realResolver = new TypeResolver(new[] { model });
    string text = new CSharpDeclarationTranslator(
        realResolver, model.Name).TranslateModuleMembers(model);
    string? line = text.Split('\n').FirstOrDefault(value =>
        value.Contains(" cPi180 = "));
    Console.WriteLine("=== Real generated constant ===");
    Console.WriteLine(line);
    Check("Real cPi180 uses double and Math.PI",
        line is not null &&
        line.Contains("public const double cPi180") &&
        line.Contains("global::System.Math.PI / 180"));
}

Console.WriteLine($"Pi constant checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;