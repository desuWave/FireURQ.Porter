using FireURQ.Porter.AST;
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
unit LifecycleTest;
interface
type
TExample = class
public
  constructor Create;
  constructor Load(value: Integer);
  destructor Destroy; override;
  procedure Reset;
  function ReadValue: Integer;
end;
TOrdinary = class
public
  procedure Destroy;
end;
implementation
constructor TExample.Create;
begin
  Reset();
end;
constructor TExample.Load(value: Integer);
begin
  Reset();
end;
destructor TExample.Destroy;
begin
  Reset();
end;
procedure TExample.Reset;
begin
end;
function TExample.ReadValue: Integer;
begin
  Result := 7;
end;
procedure TOrdinary.Destroy;
begin
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var example = unit.Declarations.OfType<DelphiClass>()
    .Single(item => item.Name == "TExample");
var ordinary = unit.Declarations.OfType<DelphiClass>()
    .Single(item => item.Name == "TOrdinary");

DelphiFunction Member(DelphiClass owner, string name) =>
    owner.Members.Select(item => item.Node).OfType<DelphiFunction>()
        .Single(item => item.Name == name);

Check("AST constructor Create",
    Member(example, "Create") is { IsConstructor: true, IsDestructor: false });
Check("AST constructor with another name",
    Member(example, "Load") is { IsConstructor: true, IsDestructor: false });
Check("AST destructor",
    Member(example, "Destroy") is { IsConstructor: false, IsDestructor: true });
Check("AST ordinary procedure named Destroy",
    Member(ordinary, "Destroy") is { IsConstructor: false, IsDestructor: false });
Check("AST ordinary procedure and function",
    !Member(example, "Reset").IsConstructor &&
    !Member(example, "Reset").IsDestructor &&
    !Member(example, "ReadValue").IsConstructor &&
    !Member(example, "ReadValue").IsDestructor);

var implementations = unit.Declarations.OfType<DelphiFunction>()
    .Where(item => item.BodyTokens.Count > 0).ToList();
Check("All six implementation bodies survive", implementations.Count == 6);
Check("Implementation constructor flags",
    implementations.Count(item => item.IsConstructor) == 2);
Check("Implementation destructor flags",
    implementations.Count(item => item.IsDestructor) == 1);

foreach (var entry in new[]
{
    (Name: "Builder", Model: SemanticModelBuilder.Build(unit)),
    (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(unit))
})
{
    var model = entry.Model;
    var members = model.Classes.Single(item => item.Name == "TExample").Members;
    Check(entry.Name + ": constructor members",
        members.Count(item => item.IsConstructor) == 2);
    Check(entry.Name + ": destructor member",
        members.Single(item => item.Name == "Destroy").IsDestructor);
    Check(entry.Name + ": ordinary Destroy remains a procedure",
        !model.Classes.Single(item => item.Name == "TOrdinary")
            .Members.Single(item => item.Name == "Destroy").IsDestructor);
    Check(entry.Name + ": implementation flags preserved",
        model.Functions.Count(item => item.IsConstructor) == 2 &&
        model.Functions.Count(item => item.IsDestructor) == 1);

    var bodies = BodyTokenAnalyzer.Analyze(unit, "LifecycleTest.pas")
        .Where(item => item.BodyTokens.Count > 0).ToList();

    int matched = 0;
    foreach (var body in bodies)
    {
        var function = FunctionImplementationMatcher.Match(unit, body, model);
        var implementation = implementations.Single(item => item.Name == body.Name);
        if (function is not null &&
            function.IsConstructor == implementation.IsConstructor &&
            function.IsDestructor == implementation.IsDestructor)
            matched++;
    }

    Check(entry.Name + ": matcher preserves all six callable kinds",
        bodies.Count == 6 && matched == 6);
}

System.Text.Encoding.RegisterProvider(
    System.Text.CodePagesEncodingProvider.Instance);
string realPath = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dClasses.pas";
string realSource = File.ReadAllText(
    realPath, System.Text.Encoding.GetEncoding(1251));
var realTokens = new DelphiLexer(realSource).Tokenize();
bool preprocessed = new DelphiConditionalPreprocessor().TryProcess(
    realTokens, out var processedTokens, out var preprocessingError, realPath);
Check("Real object base preprocessing", preprocessed);
if (!preprocessed)
{
    Console.WriteLine(preprocessingError);
    return 1;
}

var realUnit = new DelphiParser(processedTokens).ParseUnit();
foreach (var entry in new[]
{
    (Name: "Builder", Model: SemanticModelBuilder.Build(realUnit)),
    (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(realUnit))
})
{
    var primary = entry.Model.Classes.Single(
        item => item.Name == "Td2dProtoObjectPrim");
    var derived = entry.Model.Classes.Single(
        item => item.Name == "Td2dProtoObject");
    var primaryDestroy = primary.Members.Single(
        item => item.Name == "Destroy");
    var derivedDestroy = derived.Members.Single(
        item => item.Name == "Destroy");

    Check(entry.Name + ": real primary destructor is an override",
        primaryDestroy.IsDestructor &&
        primaryDestroy.Modifiers.Contains(
            "override", StringComparer.OrdinalIgnoreCase));
    Check(entry.Name + ": real protected destructor remains distinct",
        derivedDestroy.IsDestructor &&
        string.Equals(derivedDestroy.Visibility, "Protected",
            StringComparison.OrdinalIgnoreCase) &&
        !derivedDestroy.Modifiers.Contains(
            "override", StringComparer.OrdinalIgnoreCase));
    Check(entry.Name + ": NewInstance remains a class function",
        primary.Members.Single(item => item.Name == "NewInstance")
            is { IsClassMethod: true, IsConstructor: false, IsDestructor: false });

    var realBodies = BodyTokenAnalyzer.Analyze(realUnit, realPath)
        .Where(item => item.BodyTokens.Count > 0 &&
            item.Name.EndsWith(".Destroy", StringComparison.OrdinalIgnoreCase))
        .ToList();
    Check(entry.Name + ": both real destructor implementations match",
        realBodies.Count == 2 &&
        realBodies.All(body =>
            FunctionImplementationMatcher.Match(realUnit, body, entry.Model)
                is { IsDestructor: true, IsConstructor: false }));
}

Console.WriteLine($"Lifecycle-metadata checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;