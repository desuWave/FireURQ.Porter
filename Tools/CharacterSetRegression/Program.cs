using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

string source =
    "unit CharacterSetTest;\ninterface\nconst\n" +
    "Letters = ['\u0410'..'\u042F', '\u0430'..'\u044F', " +
    "'\u0401', '\u0451', '_', '0'..'9'];\n" +
    "Numbers = [0, 1, 2];\n" +
    "Controls = [#0, #9];\nimplementation\nend.\n";

string EscapeNonAscii(string text) =>
    string.Concat(text.Select(c =>
        c > 127 ? "\\u" + ((int)c).ToString("X4") : c.ToString()));

Console.WriteLine("=== Input fixture ===");
Console.WriteLine(EscapeNonAscii(source));

var model = SemanticModelBuilder.Build(
    new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit());

foreach (var constant in model.Constants)
    Console.WriteLine(
        $"{constant.Name}: {constant.Type} = {EscapeNonAscii(constant.Value)}");

string generated = new CSharpDeclarationTranslator(
    new TypeResolver(new[] { model }), model.Name).TranslateModuleMembers(model);

Console.WriteLine("=== Generated declarations ===");
Console.WriteLine(EscapeNonAscii(generated));

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("Character set inferred",
    model.Constants.Single(c => c.Name == "Letters").Type == "set of Char");
Check("Numeric set stays byte-based",
    model.Constants.Single(c => c.Name == "Numbers").Type == "set of Byte");
Check("Character code set inferred",
    model.Constants.Single(c => c.Name == "Controls").Type == "set of Char");
Check("Cyrillic code points are not cast to byte",
    !generated.Contains("(byte)1040") &&
    generated.Contains("(char)1040"));
Check("Both Yo characters preserved",
    generated.Contains("(char)1025") &&
    generated.Contains("(char)1105"));

string directory = Path.Combine(
    Path.GetTempPath(),
    "FireURQ.CharacterSetCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string project = Path.Combine(directory, "CharacterSets.csproj");

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
bool passed =
    Constants.Letters.Count == 77 &&
    Constants.Letters.Contains('\u0410') &&
    Constants.Letters.Contains('\u042F') &&
    Constants.Letters.Contains('\u0430') &&
    Constants.Letters.Contains('\u044F') &&
    Constants.Letters.Contains('\u0401') &&
    Constants.Letters.Contains('\u0451') &&
    Constants.Letters.Contains('_') &&
    Constants.Letters.Contains('0') &&
    !Constants.Letters.Contains('*') &&
    Constants.Numbers.SetEquals(new byte[] { 0, 1, 2 }) &&
    Constants.Controls.SetEquals(new char[] { '\0', '\t' });

Console.WriteLine(
    passed ? "[PASS] Generated character membership" :
             "[FAIL] Generated character membership");
return passed ? 0 : 1;

public static class Constants
{
""" + Environment.NewLine + generated + Environment.NewLine + "}");

Console.WriteLine($"Generated test project: {project}");

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

Check("Generated sets compile and preserve membership", exitCode == 0);
Console.WriteLine($"Character-set checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
