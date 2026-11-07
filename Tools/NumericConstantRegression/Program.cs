using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

string source = """
unit NumericConstantTest;
interface
const
  Negative = -1;
  Positive = +1;
  SignedMin = -2147483648;
  LowHex = $7FFFFFFF;
  HighBit = $80000000;
  FullWord = $FFFFFFFF;
  WideSigned = $100000000;
  WideUnsigned = $FFFFFFFFFFFFFFFF;
  ExplicitWide: Int64 = $FFFFFFFF;
implementation
end.
""";

var ast = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(ast);
var translator = new CSharpDeclarationTranslator(
    new TypeResolver(new[] { model }), model.Name);

string generated = translator.TranslateModuleMembers(model);
Console.WriteLine(generated);

int total = 0;
int failed = 0;

void Check(string name, string expectedType)
{
    total++;
    var constant = model.Constants.FirstOrDefault(c => c.Name == name);
    bool passed = constant is not null &&
        CSharpTypeMapper.Map(constant.Type) == expectedType;

    if (!passed) failed++;
    Console.WriteLine(
        $"[{(passed ? "PASS" : "FAIL")}] {name}: " +
        $"{constant?.Type ?? "<missing>"} -> {expectedType}");
}

Check("Negative", "int");
Check("Positive", "int");
Check("SignedMin", "int");
Check("LowHex", "int");
Check("HighBit", "uint");
Check("FullWord", "uint");
Check("WideSigned", "long");
Check("WideUnsigned", "ulong");
Check("ExplicitWide", "long");

string directory = Path.Combine(
    Path.GetTempPath(),
    "FireURQ.NumericConstantCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

string project = Path.Combine(directory, "NumericConstants.csproj");

File.WriteAllText(project, """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
""");

File.WriteAllText(Path.Combine(directory, "Program.cs"), """
bool passed =
    Constants.Negative == -1 &&
    Constants.Positive == 1 &&
    Constants.SignedMin == int.MinValue &&
    Constants.LowHex == int.MaxValue &&
    Constants.HighBit == 0x80000000u &&
    Constants.FullWord == uint.MaxValue &&
    Constants.WideSigned == 4294967296L &&
    Constants.WideUnsigned == ulong.MaxValue &&
    Constants.ExplicitWide == 4294967295L;

System.Console.WriteLine(
    passed ? "[PASS] Generated numeric values" : "[FAIL] Generated numeric values");
return passed ? 0 : 1;

public static class Constants
{
""" + Environment.NewLine + generated + Environment.NewLine + "}");

var info = new System.Diagnostics.ProcessStartInfo("dotnet")
{
    UseShellExecute = false
};
info.ArgumentList.Add("run");
info.ArgumentList.Add("--project");
info.ArgumentList.Add(project);
info.ArgumentList.Add("--configuration");
info.ArgumentList.Add("Release");

int generatedExitCode = 1;
using (var process = System.Diagnostics.Process.Start(info))
{
    if (process is not null)
    {
        process.WaitForExit();
        generatedExitCode = process.ExitCode;
    }
}

total++;
if (generatedExitCode != 0) failed++;

Console.WriteLine($"Generated compile/run exit code: {generatedExitCode}");
Console.WriteLine($"Numeric constant checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
