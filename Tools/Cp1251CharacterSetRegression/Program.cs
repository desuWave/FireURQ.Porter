using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var cp1251 = Encoding.GetEncoding(
    1251,
    EncoderFallback.ExceptionFallback,
    DecoderFallback.ExceptionFallback);

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

var examples = new[]
{
    (Character: '\u0410', Byte: (byte)0xC0),
    (Character: '\u042F', Byte: (byte)0xDF),
    (Character: '\u0430', Byte: (byte)0xE0),
    (Character: '\u044F', Byte: (byte)0xFF),
    (Character: '\u0401', Byte: (byte)0xA8),
    (Character: '\u0451', Byte: (byte)0xB8)
};

foreach (var example in examples)
{
    string decoded = cp1251.GetString(new[] { example.Byte });
    byte[] encoded = cp1251.GetBytes(example.Character.ToString());

    Check($"CP1251 byte 0x{example.Byte:X2} round-trips U+{(int)example.Character:X4}",
        decoded == example.Character.ToString() &&
        encoded.Length == 1 &&
        encoded[0] == example.Byte);
}

string path = args.Length > 0
    ? args[0]
    : @"E:\EVI_DATA\EVI_Projects\fireurq-master\furqTypes.pas";

byte[] originalBytes = File.ReadAllBytes(path);
string source = cp1251.GetString(originalBytes);

Check("Real source contains no replacement characters",
    !source.Contains('\uFFFD'));
Check("Real source byte-for-byte CP1251 round-trip",
    originalBytes.SequenceEqual(cp1251.GetBytes(source)));

var processor = new DelphiConditionalPreprocessor();

bool processed = processor.TryProcess(
    new DelphiLexer(source).Tokenize(),
    out var tokens, out var error, sourcePath: path);

Check("Real source preprocessing", processed);

if (!processed)
{
    Console.WriteLine(error);
    Console.WriteLine($"CP1251 checks: {total - failed}/{total} passed.");
    return 1;
}

var model = SemanticModelBuilder.Build(
    new DelphiParser(tokens).ParseUnit());

var selected = model.Constants.Where(constant =>
    constant.Name.Equals("cIdentStartSet", StringComparison.OrdinalIgnoreCase) ||
    constant.Name.Equals("cIdentMidSet", StringComparison.OrdinalIgnoreCase))
    .ToArray();

Check("Both real identifier sets found", selected.Length == 2);
Check("Both real identifier sets use Char",
    selected.Length == 2 &&
    selected.All(constant =>
        constant.Type.Equals("set of Char", StringComparison.OrdinalIgnoreCase)));

var fixtureModel = model with { Constants = selected };
var translator = new CSharpDeclarationTranslator(
    new TypeResolver(new[] { model }), model.Name);

string generated = translator.TranslateModuleMembers(fixtureModel);

Check("Generated sets avoid byte casts",
    !generated.Contains("(byte)") &&
    generated.Contains("HashSet<char>"));

string directory = Path.Combine(
    Path.GetTempPath(),
    "FireURQ.Cp1251Sets." + Guid.NewGuid().ToString("N"));

Directory.CreateDirectory(directory);
string project = Path.Combine(directory, "Cp1251Sets.csproj");

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
var start = new HashSet<char>();

for (int value = 'a'; value <= 'z'; value++) start.Add((char)value);
for (int value = 'A'; value <= 'Z'; value++) start.Add((char)value);
for (int value = 0x0410; value <= 0x042F; value++) start.Add((char)value);
for (int value = 0x0430; value <= 0x044F; value++) start.Add((char)value);

start.Add('_');
start.Add('\u0401');
start.Add('\u0451');

var mid = new HashSet<char>(start);
for (int value = '0'; value <= '9'; value++) mid.Add((char)value);

bool passed =
    Constants.cIdentStartSet.SetEquals(start) &&
    Constants.cIdentMidSet.SetEquals(mid) &&
    !Constants.cIdentStartSet.Contains('0') &&
    Constants.cIdentMidSet.Contains('0') &&
    !Constants.cIdentMidSet.Contains('*') &&
    !Constants.cIdentMidSet.Contains('?');

Console.WriteLine(
    $"Real identifier set sizes: start={Constants.cIdentStartSet.Count}, " +
    $"mid={Constants.cIdentMidSet.Count}");

Console.WriteLine(
    passed ? "[PASS] Real generated identifier sets preserve membership" :
             "[FAIL] Real generated identifier sets preserve membership");

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

Check("Real generated sets compile and match expected characters",
    generatedExitCode == 0);

Console.WriteLine($"Generated compile/run exit code: {generatedExitCode}");
Console.WriteLine($"CP1251 checks: {total - failed}/{total} passed.");

return failed == 0 ? 0 : 1;
