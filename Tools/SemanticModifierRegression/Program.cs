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

var unit = new DelphiParser(new DelphiLexer("""
unit SemanticModifiers;
interface
type
TExample = class
protected
  procedure Work; virtual;
  class function Make: Integer; virtual;
public
  procedure Inspect(Item: Pointer); override;
end;
implementation
procedure TExample.Work;
begin
end;
class function TExample.Make: Integer;
begin
  Result := 1;
end;
procedure TExample.Inspect(Item: Pointer);
var Alias: TExample absolute Item;
begin
end;
end.
""").Tokenize()).ParseUnit();

foreach (var entry in new[]
{
    (Name: "Builder", Model: SemanticModelBuilder.Build(unit)),
    (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(unit))
})
{
    var owner = entry.Model.Classes.Single(value => value.Name == "TExample");
    Check(entry.Name + ": member virtual preserved",
        owner.Members.Single(value => value.Name == "Work")
            .Modifiers.Contains("virtual"));
    Check(entry.Name + ": member class marker preserved",
        owner.Members.Single(value => value.Name == "Make").IsClassMethod);
    Check(entry.Name + ": implementation class marker preserved",
        entry.Model.Functions.Single(value => value.Name == "TExample.Make")
            .IsClassMethod);

    var matched = new Dictionary<string, SemanticFunction>();
    foreach (var body in BodyTokenAnalyzer.Analyze(unit, "SemanticModifiers.pas")
        .Where(value => value.BodyTokens.Count > 0))
    {
        var function = FunctionImplementationMatcher.Match(unit, body, entry.Model);
        if (function is not null)
            matched[function.Name] = function;
    }

    Check(entry.Name + ": all three implementations matched", matched.Count == 3);
    Check(entry.Name + ": declaration virtual reaches implementation",
        matched.TryGetValue("Work", out var work) &&
        work.Modifiers.Contains("virtual"));
    Check(entry.Name + ": protected visibility reaches implementation",
        work?.Visibility == "Protected");
    Check(entry.Name + ": class virtual reaches implementation",
        matched.TryGetValue("Make", out var make) &&
        make.IsClassMethod && make.Modifiers.Contains("virtual"));
    Check(entry.Name + ": override reaches implementation",
        matched.TryGetValue("Inspect", out var inspect) &&
        inspect.Modifiers.Contains("override"));
    Check(entry.Name + ": absolute target survives class matching",
        inspect is not null &&
        inspect.LocalVariables.Single(value => value.Name == "Alias")
            .AbsoluteTarget == "Item");
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dClasses.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real d2dClasses preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var real = new DelphiParser(tokens).ParseUnit();
    var model = SemanticModelBuilder.Build(real);

    foreach (var expected in new[]
    {
        (Name: "Td2dProtoObjectPrim.Cleanup", Modifier: "virtual", Class: false),
        (Name: "Td2dProtoObjectPrim.FreeInstance", Modifier: "override", Class: false),
        (Name: "Td2dProtoObjectPrim.NewInstance", Modifier: "override", Class: true)
    })
    {
        var body = BodyTokenAnalyzer.Analyze(real, path).Single(value =>
            value.Name == expected.Name && value.BodyTokens.Count > 0);
        var match = FunctionImplementationMatcher.Match(real, body, model);
        Check(expected.Name + ": matched metadata",
            match is not null &&
            match.IsClassMethod == expected.Class &&
            match.Modifiers.Contains(expected.Modifier));
        Console.WriteLine(
            $"{expected.Name}: class={match?.IsClassMethod}; " +
            $"visibility={match?.Visibility}; " +
            $"modifiers={string.Join(",", match?.Modifiers ?? Array.Empty<string>())}");
    }
}

Console.WriteLine($"Semantic-modifier checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;