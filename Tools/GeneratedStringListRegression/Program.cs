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
    if (!condition) failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string sourcePath = @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2Decorators.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        sourcePath, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: sourcePath);
Check("Real decorators preprocessing succeeds", ok);
if (!ok)
{
    Console.WriteLine(error);
    return 1;
}

var realUnit = new DelphiParser(tokens).ParseUnit();
var realModel = SemanticModelBuilder.Build(realUnit);
var realBody = BodyTokenAnalyzer.Analyze(realUnit, sourcePath).Single(body =>
    body.Name.Equals("CompareDecorators", StringComparison.OrdinalIgnoreCase) &&
    body.BodyTokens.Count > 0);
var realFunction = FunctionImplementationMatcher.Match(
    realUnit, realBody, realModel);
Check("Real decorator comparator signature matches", realFunction is not null);
if (realFunction is null)
    return 1;

string comparator = new DelphiToCSharpTranslator(
    new TypeResolver(new[] { realModel }), realModel.Name)
    .TranslateFunction(realFunction,
        BehaviorAnalyzer.Analyze(
            sourcePath, realBody.Name, realBody.BodyTokens));
Console.WriteLine("=== Generated real decorator comparator ===");
Console.WriteLine(comparator);

Check("Declared class is translated as a cast",
    comparator.Contains("((Tf2DecoratorList)(List))") &&
    !comparator.Contains("Tf2DecoratorList(List)"));

Check("Both real comparator locals are emitted",
    comparator.Contains("Tf2BaseDecorator l_D1;") &&
    comparator.Contains("Tf2BaseDecorator l_D2;"));
Check("A class declaration is not treated as a value declaration",
    !new TypeResolver(new[] { realModel }).HasVisibleNonTypeDeclaration(
        "Tf2DecoratorList", realModel.Name));

const string fixture = """
unit StringListBinding;
interface
function CompareDecorators(List: TStringList; Index1, Index2: Integer): Integer;
implementation

function MakeBare: tstringlist;
begin
  Result := TStringList.Create;
end;

function MakeExplicit: TStringList;
begin
  Result := TStringList.Create();
end;

procedure Configure(items: TStringList);
begin
  items.CaseSensitive := False;
  items.Sorted := True;
  items.Duplicates := dupError;
end;

function Attach(items: TStringList; name: string; value: TObject): Integer;
begin
  Result := items.AddObject(name, value);
end;

function ReadAttached(items: TStringList; index: Integer): TObject;
begin
  Result := items.Objects[index];
end;

procedure SortDecorators(items: TStringList);
begin
  items.CustomSort(CompareDecorators);
end;

procedure Rename(items: TStringList; index: Integer; name: string);
begin
  items.Strings[index] := name;
end;

procedure ClearStrings(items: TStringList);
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

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "StringListBinding.pas")
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
Check("All eight Pascal string-list helpers are translated", methods.Count == 8);

Console.WriteLine("=== Generated string-list helpers ===");
foreach (string method in methods.Values)
    Console.WriteLine(method);

var groupedUnit = new DelphiParser(new DelphiLexer("""
unit GroupedLocals;
interface
implementation
function SumGroups: Integer;
var
  first, second, third: Integer;
  fourth, fifth: Integer;
begin
  first := 1;
  second := 2;
  third := 3;
  fourth := 4;
  fifth := 5;
  Result := first + second + third + fourth + fifth;
end;
end.
""").Tokenize()).ParseUnit();

string[] expectedNames = { "first", "second", "third", "fourth", "fifth" };
var groupedAstFunction = groupedUnit.Declarations
    .OfType<FireURQ.Porter.AST.DelphiFunction>()
    .Single(value => value.Name == "SumGroups");
Check("AST preserves every grouped local variable",
    groupedAstFunction.LocalVariables.Select(value => value.Name)
        .SequenceEqual(expectedNames));

var groupedModel = SemanticModelBuilder.Build(groupedUnit);
var groupedAnalyzerModel = SemanticAnalyzer.Analyze(groupedUnit);
Check("Builder preserves every grouped local variable",
    groupedModel.Functions.Single(value => value.Name == "SumGroups")
        .LocalVariables.Select(value => value.Name).SequenceEqual(expectedNames));
Check("Analyzer preserves every grouped local variable",
    groupedAnalyzerModel.Functions.Single(value => value.Name == "SumGroups")
        .LocalVariables.Select(value => value.Name).SequenceEqual(expectedNames));

var groupedBody = BodyTokenAnalyzer.Analyze(groupedUnit, "GroupedLocals.pas")
    .Single(value => value.BodyTokens.Count > 0);
var groupedFunction = FunctionImplementationMatcher.Match(
    groupedUnit, groupedBody, groupedModel);
Check("Matcher preserves all five local variables",
    groupedFunction is not null && groupedFunction.LocalVariables.Count == 5);

string groupedTranslation = groupedFunction is null ? string.Empty :
    new DelphiToCSharpTranslator(
        new TypeResolver(new[] { groupedModel }), groupedModel.Name)
    .TranslateFunction(groupedFunction,
        BehaviorAnalyzer.Analyze(
            groupedBody.FilePath, groupedBody.Name, groupedBody.BodyTokens));

var shadowUnit = new DelphiParser(new DelphiLexer("""
unit ShadowCast;
interface
type
  Tf2DecoratorList = class
  end;
  TShadowCallback = function(value: Integer): Integer;
implementation
function CallShadow(Tf2DecoratorList: TShadowCallback): Integer;
begin
  Result := Tf2DecoratorList(7);
end;
end.
""").Tokenize()).ParseUnit();

var shadowModel = SemanticModelBuilder.Build(shadowUnit);
var shadowResolver = new TypeResolver(new[] { shadowModel });
var shadowBody = BodyTokenAnalyzer.Analyze(shadowUnit, "ShadowCast.pas")
    .Single(value => value.BodyTokens.Count > 0);
var shadowFunction = FunctionImplementationMatcher.Match(
    shadowUnit, shadowBody, shadowModel);
string shadowTranslation = shadowFunction is null ? string.Empty :
    new DelphiToCSharpTranslator(shadowResolver, shadowModel.Name)
    .TranslateFunction(shadowFunction,
        BehaviorAnalyzer.Analyze(
            shadowBody.FilePath, shadowBody.Name, shadowBody.BodyTokens));
Check("Callback parameter shadows the class name",
    shadowTranslation.Contains("result = Tf2DecoratorList(7);"));

if (failed == 0)
{
    string directory = Path.Combine(
        Path.GetTempPath(), $"FireURQ.GeneratedStringList.{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var encoding = new UTF8Encoding(false);

    File.WriteAllText(Path.Combine(directory, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""", encoding);

    foreach (var item in new[]
    {
        (Name: "Objects.cs", Source: CSharpObjectRuntime.Source),
        (Name: "StringLists.cs", Source: CSharpStringListRuntime.Source),
        (Name: "StringListCommon.cs", Source: CSharpStringListCommonRuntime.Source)
    })
        File.WriteAllText(Path.Combine(directory, item.Name), item.Source, encoding);

    File.WriteAllText(Path.Combine(directory, "Generated.cs"),
        "using System;\nusing FireURQ.Runtime;\n" +
        "using static FireURQ.Runtime.TDuplicates;\n" +
        "public delegate int TShadowCallback(int value);\n" +
        "public static class Generated\n{\npublic static " + comparator + "\n" +
        string.Join("\n", methods.Values.Select(value => "public static " + value)) +
        "\npublic static " + groupedTranslation +
        "\npublic static " + shadowTranslation +
        "\n}\n", encoding);

    File.WriteAllText(Path.Combine(directory, "ProbeTypes.cs"), """
using FireURQ.Runtime;

public sealed class Tf2BaseDecorator : TObject
{
    public float PosZ;
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

public sealed class Tf2DecoratorList : TStringList
{
    public DecoratorItems Decorators { get; }
    public Tf2DecoratorList() => Decorators = new DecoratorItems(this);

    public sealed class DecoratorItems
    {
        private readonly Tf2DecoratorList owner;
        internal DecoratorItems(Tf2DecoratorList owner) => this.owner = owner;
        public Tf2BaseDecorator this[int index] =>
            (Tf2BaseDecorator)owner.Objects[index]!;
    }
}
""", encoding);

    File.WriteAllText(Path.Combine(directory, "Program.cs"), """
using FireURQ.Runtime;

int passed = 0;
int failed = 0;
void Check(string name, bool condition)
{
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
    if (condition) passed++; else failed++;
}

TStringList scenes = Generated.MakeBare();
TStringList other = Generated.MakeExplicit();
Check("Generated factories create independent empty lists",
    scenes.Count == 0 && other.Count == 0 && !ReferenceEquals(scenes, other));
Generated.Configure(scenes);
var alpha = new Tf2BaseDecorator();
var beta = new Tf2BaseDecorator();
Generated.Attach(scenes, "beta", beta);
int index = Generated.Attach(scenes, "alpha", alpha);
Check("Generated AddObject inserts complete pairs in sorted order",
    index == 0 && scenes[0] == "alpha" &&
    ReferenceEquals(Generated.ReadAttached(scenes, 0), alpha));

bool duplicateRejected = false;
try
{
    Generated.Attach(scenes, "ALPHA", beta);
}
catch (ArgumentException)
{
    duplicateRejected = true;
}
Check("Generated dupError configuration rejects a duplicate without mutation",
    duplicateRejected && scenes.Count == 2 &&
    ReferenceEquals(Generated.ReadAttached(scenes, 0), alpha));

Generated.Attach(other, "old", alpha);
Generated.Rename(other, 0, "renamed");
Check("Generated Strings assignment preserves the associated object",
    other[0] == "renamed" &&
    ReferenceEquals(Generated.ReadAttached(other, 0), alpha));
Generated.ClearStrings(other);
Generated.ClearStrings(scenes);
Check("Generated Clear leaves associated objects alive",
    alpha.Destroys == 0 && beta.Destroys == 0);
TObject.Free(scenes);
TObject.Free(other);
TObject.Free(alpha);
TObject.Free(beta);

var decorators = new Tf2DecoratorList();
var values = new[]
{
    new Tf2BaseDecorator { PosZ = -2 },
    new Tf2BaseDecorator { PosZ = 7 },
    new Tf2BaseDecorator { PosZ = 0 },
    new Tf2BaseDecorator { PosZ = 3 }
};
for (int item = 0; item < values.Length; item++)
    decorators.AddObject("item-" + item, values[item]);

Generated.SortDecorators(decorators);
Check("Real translated comparator sorts by descending PosZ",
    Enumerable.Range(0, decorators.Count)
        .Select(item => decorators.Decorators[item].PosZ)
        .SequenceEqual(new float[] { 7, 3, 0, -2 }));
Check("CustomSort retains the original string-object associations",
    Enumerable.Range(0, decorators.Count).All(item =>
        ReferenceEquals(decorators.Objects[item],
            values[int.Parse(decorators[item]["item-".Length..])])));

TObject.Free(decorators);
Check("Base string-list destruction preserves borrowed decorators",
    values.All(value => value.Destroys == 0));
foreach (var value in values)
    TObject.Free(value);
Check("Caller destroys every borrowed decorator exactly once",
    values.All(value => value.Destroys == 1 && value.Completions == 1));

Check("Generated grouped locals execute with all five values",
    Generated.SumGroups() == 15);
Check("Generated shadowing callback remains a call",
    Generated.CallShadow(value => value + 3) == 10);

Console.WriteLine($"Generated string-list execution: {passed}/{passed + failed} passed.");
return failed == 0 ? 0 : 1;
""", encoding);

    var start = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = directory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (string argument in new[]
    {
        "run", "--project", Path.Combine(directory, "Probe.csproj"),
        "--configuration", "Release", "--verbosity", "quiet"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start);
    if (process is null)
    {
        Check("Generated string-list helpers compile and execute", false);
    }
    else
    {
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.Write(await output);
        Console.Write(await errors);
        Check("Generated string-list helpers compile and execute", process.ExitCode == 0);
    }
    Console.WriteLine($"Generated compilation directory: {directory}");
}

Console.WriteLine($"Generated-string-list checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;