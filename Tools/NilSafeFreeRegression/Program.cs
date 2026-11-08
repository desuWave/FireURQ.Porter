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

string source = """
unit NilFreeTest;
interface
type
TTracked = class
public
  Destructions: Integer;
  Releases: Integer;
  destructor Destroy; override;
protected
  procedure FreeInstance; override;
end;
TLeaf = class(TTracked)
end;
TCustom = class
public
  Calls: Integer;
  procedure Free;
end;
TCustomLeaf = class(TCustom)
end;
procedure ReleaseBase(value: TObject);
procedure ReleaseTyped(value: TLeaf);
procedure ReleaseLocal;
procedure ReleaseCustom(value: TCustom);
implementation
destructor TTracked.Destroy;
begin
  Destructions := Destructions + 1;
  inherited;
end;
procedure TTracked.FreeInstance;
begin
  Releases := Releases + 1;
  inherited;
end;
procedure TCustom.Free;
begin
  Calls := Calls + 1;
end;
procedure ReleaseBase(value: TObject);
begin
  value.Free;
end;
procedure ReleaseTyped(value: TLeaf);
begin
  value.Free();
end;
procedure ReleaseLocal;
var
  value: TLeaf;
begin
  value := nil;
  value.Free;
end;
procedure ReleaseCustom(value: TCustom);
begin
  value.Free;
end;
end.
""";

string directory = Path.Combine(
    Path.GetTempPath(), "FireURQ.NilSafeFree." +
    Guid.NewGuid().ToString("N"));
string inputRoot = Path.Combine(directory, "Input");
string outputRoot = Path.Combine(directory, "Generated");
Directory.CreateDirectory(inputRoot);
string path = Path.Combine(inputRoot, "NilFreeTest.pas");
File.WriteAllText(path, source);

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
Check("Standard TObject.Free recognized",
    resolver.IsInheritedTObjectFree("TObject", model.Name));
Check("Inherited TObject.Free recognized",
    resolver.IsInheritedTObjectFree("TLeaf", model.Name));
Check("Custom Free remains distinct",
    !resolver.IsInheritedTObjectFree("TCustom", model.Name));
Check("Inherited custom Free remains distinct",
    !resolver.IsInheritedTObjectFree("TCustomLeaf", model.Name));
Check("Unknown types are not assumed to be TObject",
    !resolver.IsInheritedTObjectFree("TUnknown", model.Name));
Check("Pointers are excluded",
    !resolver.IsInheritedTObjectFree("Pointer", model.Name));

var aliasModel = model with
{
    Types = model.Types.Concat(new[]
    {
        new SemanticType("TObjectAlias", "Simple", "TObject")
    }).ToArray()
};
Check("Alias to TObject is recognized",
    new TypeResolver(new[] { aliasModel })
        .IsInheritedTObjectFree("TObjectAlias", model.Name));

var bodies = BodyTokenAnalyzer.Analyze(unit, path)
    .Where(body => body.BodyTokens.Count > 0)
    .Select(body => (
        Body: body,
        Behavior: BehaviorAnalyzer.Analyze(path, body.Name, body.BodyTokens)))
    .ToList();
var input = new ProjectTranslationInput(inputRoot, path, unit, model, bodies);
var results = new ProjectTranslator().Translate(new[] { input }, outputRoot);
Check("Project translation succeeds",
    results.Count == 1 && results[0].Success);
if (results.Count != 1 || !results[0].Success)
{
    foreach (var result in results) Console.WriteLine(result.Error);
    return 1;
}

string generated = File.ReadAllText(results[0].OutputFile);
Console.WriteLine("=== Generated Free calls ===");
foreach (string line in generated.Split('\n').Where(line =>
    line.Contains(".Free(")))
    Console.WriteLine(line.Trim());

Check("Three standard Free calls use the null-safe helper",
    generated.Split("global::FireURQ.Runtime.TObject.Free(value)").Length - 1 == 3);
Check("Custom Free remains an instance call",
    generated.Split("value.Free();").Length - 1 == 1);
Check("All seven implementation bodies emitted",
    results[0].FunctionsTranslated == 7);

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
public static class Program
{
    public static int Main()
    {
        NilFreeTestModule.ReleaseBase(null);
        NilFreeTestModule.ReleaseTyped(null);
        NilFreeTestModule.ReleaseLocal();
        System.Console.WriteLine(
            "[PASS] Base, derived and local nil receivers are accepted");

        var first = new TLeaf();
        NilFreeTestModule.ReleaseBase(first);
        var second = new TLeaf();
        NilFreeTestModule.ReleaseTyped(second);
        bool released =
            first.Destructions == 1 && first.Releases == 1 &&
            second.Destructions == 1 && second.Releases == 1;

        var custom = new TCustom();
        NilFreeTestModule.ReleaseCustom(custom);
        var customLeaf = new TCustomLeaf();
        NilFreeTestModule.ReleaseCustom(customLeaf);
        bool customPreserved = custom.Calls == 1 && customLeaf.Calls == 1;

        System.Console.WriteLine(released
            ? "[PASS] Standard Free executes destruction and completion once"
            : "[FAIL] Standard Free behavior");
        System.Console.WriteLine(customPreserved
            ? "[PASS] Custom Free and inherited custom Free execute unchanged"
            : "[FAIL] Custom Free behavior");
        return released && customPreserved ? 0 : 1;
    }
}
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
        Check("Generated nil-safe Free project compiles and executes", false);
    else
    {
        process.WaitForExit();
        Check("Generated nil-safe Free project compiles and executes",
            process.ExitCode == 0);
    }
}

Console.WriteLine($"Nil-safe-Free checks: {total - failed}/{total} passed.");
Console.WriteLine("Generated compilation directory: " + directory);
return failed == 0 ? 0 : 1;