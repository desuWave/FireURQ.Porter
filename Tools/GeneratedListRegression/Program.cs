using System.Diagnostics;
using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool condition)
{
    total++;
    if (!condition)
        failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string sourcePath = @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2ScnMain.pas";
bool preprocessed = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        sourcePath, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: sourcePath);
Check("Real scene preprocessing succeeds", preprocessed);
if (!preprocessed)
{
    Console.WriteLine(error);
    return 1;
}

var realUnit = new DelphiParser(tokens).ParseUnit();
var realModel = SemanticModelBuilder.Build(realUnit);
var realBody = BodyTokenAnalyzer.Analyze(realUnit, sourcePath).Single(body =>
    body.Name.Equals("CompareDecorSprites", StringComparison.OrdinalIgnoreCase) &&
    body.BodyTokens.Count > 0);
var realFunction = FunctionImplementationMatcher.Match(
    realUnit, realBody, realModel);
Check("Real comparator implementation matches its declaration",
    realFunction is not null);
if (realFunction is null)
    return 1;

string comparator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { realModel }), realModel.Name)
    .TranslateFunction(realFunction,
        BehaviorAnalyzer.Analyze(
            sourcePath, realBody.Name, realBody.BodyTokens));

Check("Real comparator reads both object-handle targets",
    comparator.Contains("GCHandle.FromIntPtr(Item1).Target!") &&
    comparator.Contains("GCHandle.FromIntPtr(Item2).Target!"));
Console.WriteLine("=== Generated real comparator ===");
Console.WriteLine(comparator);

const string fixture = """
unit ListBindingTest;
interface
function CompareDecorSprites(Item1, Item2: Pointer): Integer;
implementation

function MakePointers: TList;
begin
  Result := TList.Create();
end;

function MakeObjects(owns: Boolean): TObjectList;
begin
  Result := TObjectList.Create(owns);
end;

function AddPointer(items: TList; value: Pointer): Integer;
begin
  Result := items.Add(value);
end;

function AddObject(items: TObjectList; value: TObject): Integer;
begin
  Result := items.Add(value);
end;

function RemoveObject(items: TObjectList; value: TObject): Integer;
begin
  Result := items.Remove(value);
end;

function ExtractObject(items: TObjectList; value: TObject): TObject;
begin
  Result := items.Extract(value);
end;

procedure MoveObject(items: TObjectList; fromIndex, toIndex: Integer);
begin
  items.Move(fromIndex, toIndex);
end;

procedure SortSprites(items: TObjectList);
begin
  items.Sort(CompareDecorSprites);
end;

procedure ClearObjects(items: TObjectList);
begin
  items.Clear;
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(fixture).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var translator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { model }), model.Name);
var methods = new Dictionary<string, string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "ListBindingTest.pas")
    .Where(value => value.BodyTokens.Count > 0))
{
    var function = FunctionImplementationMatcher.Match(unit, body, model);
    Check(body.Name + ": signature matched", function is not null);
    if (function is null)
        continue;

    methods[function.Name] = translator.TranslateFunction(
        function,
        BehaviorAnalyzer.Analyze(body.FilePath, body.Name, body.BodyTokens));
}

Check("All nine Pascal list helpers are translated", methods.Count == 9);
Console.WriteLine("=== Generated list helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

if (failed == 0)
{
    string compileDirectory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedLists.{Guid.NewGuid():N}");
    Directory.CreateDirectory(compileDirectory);
    var encoding = new UTF8Encoding(false);

    File.WriteAllText(Path.Combine(compileDirectory, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""", encoding);

    File.WriteAllText(Path.Combine(compileDirectory, "Objects.cs"),
        CSharpObjectRuntime.Source, encoding);
    File.WriteAllText(Path.Combine(compileDirectory, "Lists.cs"),
        CSharpListRuntime.Source, encoding);

    string helpers = """
using System;
using FireURQ.Runtime;
public static class Generated
{
""" + Environment.NewLine + "public static " + comparator +
        Environment.NewLine +
        string.Join(Environment.NewLine,
            methods.Values.Select(value => "public static " + value)) +
        Environment.NewLine + "}";

    File.WriteAllText(Path.Combine(compileDirectory, "Generated.cs"),
        helpers, encoding);

    File.WriteAllText(Path.Combine(compileDirectory, "Program.cs"), """
using FireURQ.Runtime;

int passed = 0;
int failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition)
        passed++;
    else
        failed++;
}

TList pointers = Generated.MakePointers();
IntPtr marker = new(-1);
Check("Generated pointer factory and Add preserve native bits",
    Generated.AddPointer(pointers, marker) == 0 &&
    pointers.Items[0] == marker);
TObject.Free(pointers);

TObjectList owned = Generated.MakeObjects(true);
TObjectList borrowed = Generated.MakeObjects(false);
Check("Generated object factories preserve the ownership argument",
    owned.OwnsObjects && !borrowed.OwnsObjects);

var detached = new Tf2DecorationSprite { Z = 5 };
Generated.AddObject(owned, detached);
Generated.AddObject(borrowed, detached);
TObject extracted = Generated.ExtractObject(owned, detached);
Generated.RemoveObject(borrowed, detached);
Check("Generated GUI-style detach preserves the object",
    ReferenceEquals(extracted, detached) &&
    owned.Count == 0 && borrowed.Count == 0 &&
    detached.Destroys == 0);
TObject.Free(extracted);
Check("Detached object completes destruction exactly once",
    detached.Destroys == 1 && detached.Completions == 1);

var low = new Tf2DecorationSprite { Z = -2 };
var high = new Tf2DecorationSprite { Z = 7 };
var zero = new Tf2DecorationSprite { Z = 0 };
var middle = new Tf2DecorationSprite { Z = 3 };
foreach (var sprite in new[] { low, high, zero, middle })
    Generated.AddObject(owned, sprite);

Generated.MoveObject(owned, 0, 3);
Check("Generated Move changes order without destruction",
    ReferenceEquals(owned[3], low) &&
    new[] { low, high, zero, middle }.All(value => value.Destroys == 0));

GC.Collect();
GC.WaitForPendingFinalizers();
Generated.SortSprites(owned);

Check("Real translated comparator sorts by descending Z",
    Enumerable.Range(0, owned.Count)
        .Select(index => ((Tf2DecorationSprite)owned[index]!).Z)
        .SequenceEqual(new float[] { 7, 3, 0, -2 }));
Check("Sorting retains the original object instances",
    ReferenceEquals(owned[0], high) &&
    ReferenceEquals(owned[1], middle) &&
    ReferenceEquals(owned[2], zero) &&
    ReferenceEquals(owned[3], low));

Generated.ClearObjects(owned);
Check("Generated Clear destroys every owned sprite exactly once",
    owned.Count == 0 &&
    new[] { low, high, zero, middle }.All(value =>
        value.Destroys == 1 && value.Completions == 1));

TObject.Free(owned);
TObject.Free(borrowed);
Check("Freeing cleared lists does not destroy their former objects again",
    new[] { low, high, zero, middle, detached }.All(value =>
        value.Destroys == 1 && value.Completions == 1));

Console.WriteLine($"Generated list execution: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;

public sealed class Tf2DecorationSprite : TObject
{
    public float Z;
    public int Destroys;
    public int Completions;

    public override void Destroy()
    {
        using var scope = __FireUrqEnterDestructor();
        Destroys++;
        base.Destroy();
    }

    protected override void FreeInstance()
    {
        Completions++;
        base.FreeInstance();
    }
}
""", encoding);

    var start = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = compileDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (string argument in new[]
    {
        "run", "--project", Path.Combine(compileDirectory, "Probe.csproj"),
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Generated list project compiles and executes", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated list project compiles and executes",
            process.ExitCode == 0);
    }
    Console.WriteLine($"Generated compilation directory: {compileDirectory}");
}

Console.WriteLine($"Generated-list checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;