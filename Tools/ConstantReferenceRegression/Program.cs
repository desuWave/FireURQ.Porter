using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

string source = """
unit ConstantReferenceTest;
interface
const
  Base = 1;
  Alias = Base;
  Chain = Alias;
  Other = 2;
  Flags = Base or Other;
  High = $80000000;
  HighAlias = High;
  WideError = Int64(-1);
  WordError = Cardinal(-1);
  Explicit: Int64 = Base;
implementation
end.
""";

var model = SemanticModelBuilder.Build(
    new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit());

string generated = new CSharpDeclarationTranslator(
    new TypeResolver(new[] { model }), model.Name).TranslateModuleMembers(model);

Console.WriteLine(generated);

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("Reference type inferred",
    generated.Contains("public const int Alias = Base;"));
Check("Reference chain type inferred",
    generated.Contains("public const int Chain = Alias;"));
Check("Same-type bitwise expression inferred",
    generated.Contains("public const int Flags = Base | Other;"));
Check("Unsigned reference preserves uint",
    generated.Contains("public const uint HighAlias = High;"));
Check("Int64 cast inferred",
    generated.Contains("public const long WideError = unchecked((long)(- 1));"));
Check("Cardinal cast inferred",
    generated.Contains("public const uint WordError = unchecked((uint)(- 1));"));
Check("Explicit type preserved",
    generated.Contains("public const long Explicit = Base;"));

string directory = Path.Combine(
    Path.GetTempPath(),
    "FireURQ.ConstantReferenceCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string project = Path.Combine(directory, "Constants.csproj");

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
    Constants.Alias == 1 &&
    Constants.Chain == 1 &&
    Constants.Flags == 3 &&
    Constants.HighAlias == 0x80000000u &&
    Constants.WideError == -1L &&
    Constants.WordError == uint.MaxValue &&
    Constants.Explicit == 1L;

System.Console.WriteLine(
    passed ? "[PASS] Generated reference values" : "[FAIL] Generated reference values");
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

int exitCode = 1;
using (var process = System.Diagnostics.Process.Start(info))
{
    if (process is not null)
    {
        process.WaitForExit();
        exitCode = process.ExitCode;
    }
}

Check("Generated constants compile and execute", exitCode == 0);
Console.WriteLine($"Constant reference checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
