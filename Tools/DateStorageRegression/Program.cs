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
SemanticUnit Parse(string source) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit());

var unit = Parse("""
unit DateTest;
interface
type
TSaveDate = TDateTime;
TSaveRecord = record
  Stamp: TDateTime;
end;
const Epoch: TDateTime = 0;
implementation
end.
""");
var shadow = Parse("""
unit Shadow;
interface
type TDateTime = Integer;
implementation
end.
""");
var resolver = new TypeResolver(new[] { unit, shadow });
Check("Standard TDateTime maps to double",
    CSharpTypeMapper.Map("TDateTime", resolver, "DateTest") == "double");
Check("Legacy mapping ignores case",
    CSharpTypeMapper.Map("tdatetime") == "double");
Check("Alias preserves date storage",
    CSharpTypeMapper.Map("TSaveDate", resolver, "DateTest") == "double");
Check("Date array uses double elements",
    CSharpTypeMapper.Map("array of TDateTime", resolver, "DateTest") == "double[]");
Check("Explicit local declaration takes precedence",
    CSharpTypeMapper.Map("TDateTime", resolver, "Shadow") == "int");

var translator = new CSharpDeclarationTranslator(resolver, unit.Name);
string declarations = translator.Translate(unit);
string members = translator.TranslateModuleMembers(unit);
Console.WriteLine(declarations);
Console.WriteLine(members);
Check("Generated record stores date as double",
    declarations.Contains("public double Stamp;"));
Check("Generated typed constant uses double",
    members.Contains("public const double Epoch = 0;"));

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.DateCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "Dates.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText(Path.Combine(directory, "Program.cs"),
    "using System;\n" + declarations +
    "\npublic static class DateConstants {\n" + members + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        bool passed = DateConstants.Epoch == 0.0;
        foreach (double value in new[] { 0.0, 0.5, -1.25, 45000.123456789 })
        {
            var record = new TSaveRecord { Stamp = value };
            using var stream = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(
                stream, System.Text.Encoding.UTF8, true))
                writer.Write(record.Stamp);

            passed &= stream.Length == 8;
            passed &= stream.ToArray().AsSpan().SequenceEqual(
                BitConverter.GetBytes(value));
            stream.Position = 0;
            using var reader = new System.IO.BinaryReader(stream);
            passed &= BitConverter.DoubleToInt64Bits(reader.ReadDouble()) ==
                BitConverter.DoubleToInt64Bits(value);
        }
        Console.WriteLine(passed
            ? "[PASS] Generated date storage preserves all eight bytes"
            : "[FAIL] Generated date storage");
        return passed ? 0 : 1;
    }
}
""");
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(directory, "Dates.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Generated date storage compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated date storage compiles and executes", process.ExitCode == 0);
    }
}
Console.WriteLine($"Date-storage checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;