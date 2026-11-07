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

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2ScnMain.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real scene preprocessing", ok);
if (!ok)
{
    Console.WriteLine(error);
    return 1;
}

var unit = new DelphiParser(tokens).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
var body = BodyTokenAnalyzer.Analyze(unit, path).Single(value =>
    value.Name.Equals("CompareDecorSprites", StringComparison.OrdinalIgnoreCase) &&
    value.BodyTokens.Count > 0);
var function = FunctionImplementationMatcher.Match(unit, body, model);
Check("Real comparator signature found", function is not null);
if (function is null)
    return 1;

string generated = new DelphiToCSharpTranslator(resolver, model.Name)
    .TranslateFunction(function,
        BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens));
Console.WriteLine("=== Generated real comparator ===");
Console.WriteLine(generated);

Check("Both parameters remain IntPtr",
    generated.Contains("IntPtr Item1, IntPtr Item2"));
Check("Both absolute locals resolve object handles",
    generated.Contains("GCHandle.FromIntPtr(Item1).Target!") &&
    generated.Contains("GCHandle.FromIntPtr(Item2).Target!"));
Check("No raw absolute syntax remains",
    !generated.Contains(" absolute ", StringComparison.OrdinalIgnoreCase));

string compileDirectory = Path.Combine(
    Path.GetTempPath(), "FireURQ.SpriteCompile." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(compileDirectory);
File.WriteAllText(Path.Combine(compileDirectory, "Sprites.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

File.WriteAllText(Path.Combine(compileDirectory, "Program.cs"),
    "using System;\nusing System.Runtime.InteropServices;\n" +
    "public sealed class Tf2DecorationSprite { public float Z; }\n" +
    "public static class Generated {\npublic static " + generated + "\n}\n" +
    """
public static class Program
{
    public static int Main()
    {
        int allocated = 0;
        int released = 0;
        int Compare(Tf2DecorationSprite left, Tf2DecorationSprite right)
        {
            GCHandle first = default;
            GCHandle second = default;
            try
            {
                first = GCHandle.Alloc(left);
                allocated++;
                second = GCHandle.Alloc(right);
                allocated++;
                return Generated.CompareDecorSprites(
                    GCHandle.ToIntPtr(first), GCHandle.ToIntPtr(second));
            }
            finally
            {
                if (second.IsAllocated) { second.Free(); released++; }
                if (first.IsAllocated) { first.Free(); released++; }
            }
        }

        var low = new Tf2DecorationSprite { Z = -2 };
        var high = new Tf2DecorationSprite { Z = 7 };
        bool passed = Compare(low, high) == 1 &&
            Compare(high, low) == -1 && Compare(high, high) == 0;

        var sprites = new System.Collections.Generic.List<Tf2DecorationSprite>
        {
            low, high, new() { Z = 0 }, new() { Z = 3 }
        };
        sprites.Sort(Compare);
        passed &= sprites.Select(value => value.Z)
            .SequenceEqual(new float[] { 7, 3, 0, -2 });
        passed &= ReferenceEquals(sprites[0], high) &&
            ReferenceEquals(sprites[^1], low);
        passed &= allocated == released;

        Console.WriteLine(passed
            ? "[PASS] Real generated comparator sorts original objects and releases handles"
            : "[FAIL] Real generated comparator behavior");
        Console.WriteLine($"Handles allocated={allocated}; released={released}");
        return passed ? 0 : 1;
    }
}
""");
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
start.ArgumentList.Add("run");
start.ArgumentList.Add("--project");
start.ArgumentList.Add(Path.Combine(compileDirectory, "Sprites.csproj"));
start.ArgumentList.Add("--configuration");
start.ArgumentList.Add("Release");
using (var process = Process.Start(start))
{
    if (process is null)
        Check("Real generated comparator compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Real generated comparator compiles and executes", process.ExitCode == 0);
    }
}

Console.WriteLine($"Sprite-comparator checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + compileDirectory);
return failed == 0 ? 0 : 1;