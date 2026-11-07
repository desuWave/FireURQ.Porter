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

SemanticUnit Parse(string text) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(text).Tokenize()).ParseUnit());

var source = Parse(
    "unit Source; interface const Letters = ['A'..'C']; implementation end.");
var other = Parse(
    "unit Other; interface const Letters = ['Z']; implementation end.");
var consumer = Parse(
    "unit Consumer; interface uses Source; " +
    "const Masked = letters + ['*', '?']; implementation end.");
var local = Parse(
    "unit Local; interface uses Source; const Letters = ['X']; " +
    "Masked = Letters + ['*']; implementation end.");
var conflict = Parse(
    "unit Conflict; interface uses Source, Other; implementation end.");
var hidden = Parse(
    "unit Hidden; interface implementation end.");
var indirect = Parse(
    "unit Indirect; interface uses Consumer; implementation end.");

var resolver = new TypeResolver(
    new[] { source, other, consumer, local, conflict, hidden, indirect });

Check("Imported constant resolves with declaration spelling",
    resolver.TryResolveConstant("letters", "Consumer", out var target, out var owner) &&
    target?.Name == "Letters" && owner == "Source");

Check("Local constant precedes imports",
    resolver.TryResolveConstant("Letters", "Local", out _, out owner) &&
    owner == "Local");

Check("Conflicting imports are not silently selected",
    !resolver.TryResolveConstant("Letters", "Conflict", out _, out _));

Check("Unimported constant is not selected",
    !resolver.TryResolveConstant("Letters", "Hidden", out _, out _));

Check("Transitive imports are not exposed",
    !resolver.TryResolveConstant("Letters", "Indirect", out _, out _));

Check("Qualified name ignores case and spacing",
    resolver.TryResolveConstant("source . letters", "Hidden", out _, out owner) &&
    owner == "Source");

string Emit(SemanticUnit unit) =>
    new CSharpDeclarationTranslator(resolver, unit.Name)
        .TranslateModuleMembers(unit);

string sourceCode = Emit(source);
string consumerCode = Emit(consumer);
string localCode = Emit(local);

Console.WriteLine("=== Generated imported union ===");
Console.WriteLine(consumerCode);

Check("Imported union has character-set type",
    consumerCode.Contains("public static readonly HashSet<char> Masked"));

Check("Imported reference is qualified",
    consumerCode.Contains("global::SourceModule.Letters"));

Check("Local union references local declaration",
    localCode.Contains("global::LocalModule.Letters"));

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.SetUnionCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

File.WriteAllText(Path.Combine(directory, "Union.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");

string generated =
    "using System;\nusing System.Collections.Generic;\n" +
    "public static class SourceModule {\n" + sourceCode + "\n}\n" +
    "public static class ConsumerModule {\n" + consumerCode + "\n}\n" +
    "public static class LocalModule {\n" + localCode + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        bool passed =
            ConsumerModule.Masked.SetEquals(new[] { 'A', 'B', 'C', '*', '?' }) &&
            SourceModule.Letters.SetEquals(new[] { 'A', 'B', 'C' }) &&
            LocalModule.Masked.SetEquals(new[] { 'X', '*' }) &&
            LocalModule.Letters.SetEquals(new[] { 'X' }) &&
            !ReferenceEquals(ConsumerModule.Masked, SourceModule.Letters);

        ConsumerModule.Masked.Add('!');
        passed &= !SourceModule.Letters.Contains('!');

        Console.WriteLine(passed
            ? "[PASS] Union membership and source independence"
            : "[FAIL] Union membership or source independence");
        return passed ? 0 : 1;
    }
}
""";

File.WriteAllText(Path.Combine(directory, "Program.cs"), generated);

var start = new ProcessStartInfo("dotnet")
{
    UseShellExecute = false
};
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Union.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");

using (var process = Process.Start(start))
{
    if (process is null)
    {
        Check("Generated union compiles and executes", false);
    }
    else
    {
        process.WaitForExit();
        Check("Generated union compiles and executes", process.ExitCode == 0);
    }
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string[] paths =
{
    @"E:\EVI_DATA\EVI_Projects\fireurq-master\furqTypes.pas",
    @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2Interpreter.pas"
};
var realUnits = new List<SemanticUnit>();
var processor = new DelphiConditionalPreprocessor();

foreach (string path in paths)
{
    bool ok = processor.TryProcess(
        new DelphiLexer(File.ReadAllText(
            path, Encoding.GetEncoding(1251))).Tokenize(),
        out var tokens, out var error, sourcePath: path);

    Check("Preprocess " + Path.GetFileName(path), ok);
    if (!ok)
    {
        Console.WriteLine(error);
        continue;
    }

    realUnits.Add(SemanticModelBuilder.Build(
        new DelphiParser(tokens).ParseUnit()));
}

if (realUnits.Count == 2)
{
    var realResolver = new TypeResolver(realUnits);
    var interpreter = realUnits.Single(unit =>
        unit.Name.Equals("f2Interpreter", StringComparison.OrdinalIgnoreCase));

    string text = new CSharpDeclarationTranslator(
        realResolver, interpreter.Name).TranslateModuleMembers(interpreter);

    string? line = text.Split('\n').FirstOrDefault(value =>
        value.Contains(" cMaskedIdentSet = "));

    Console.WriteLine("=== Real generated union ===");
    Console.WriteLine(line);

    Check("Real masked identifier set uses character union",
        line is not null &&
        line.Contains("static readonly HashSet<char>") &&
        line.Contains("global::furqTypesModule.cIdentMidSet") &&
        line.Contains("Enumerable.Concat"));
}

Console.WriteLine($"Set-union checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;