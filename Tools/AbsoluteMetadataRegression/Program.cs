using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

var unit = new DelphiParser(new DelphiLexer("""
unit AbsoluteTest;
interface
implementation
procedure Examine(Item: Pointer);
var
  Alias: TExample absolute Item;
  MixedCase: TExample AbSoLuTe Item;
  Plain: Integer;
begin
  Plain := 1;
end;
end.
""").Tokenize()).ParseUnit();

var function = unit.Declarations.OfType<DelphiFunction>()
    .Single(value => value.Name == "Examine");
Check("AST keeps all three locals", function.LocalVariables.Count == 3);
Check("AST separates alias type and target",
    function.LocalVariables.Any(value =>
        value.Name == "Alias" &&
        value.Type == "TExample" &&
        value.AbsoluteTarget == "Item"));
Check("Absolute keyword ignores case",
    function.LocalVariables.Any(value =>
        value.Name == "MixedCase" &&
        value.Type == "TExample" &&
        value.AbsoluteTarget == "Item"));
Check("Ordinary local has no absolute target",
    function.LocalVariables.Any(value =>
        value.Name == "Plain" &&
        value.Type == "Integer" &&
        value.AbsoluteTarget is null));

foreach (var entry in new[]
{
    (Name: "Builder", Model: SemanticModelBuilder.Build(unit)),
    (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(unit))
})
{
    var semantic = entry.Model.Functions.Single(value => value.Name == "Examine");
    Check(entry.Name + ": alias metadata preserved",
        semantic.LocalVariables.Any(value =>
            value.Name == "Alias" &&
            value.Type == "TExample" &&
            value.AbsoluteTarget == "Item"));
    Check(entry.Name + ": ordinary local unchanged",
        semantic.LocalVariables.Any(value =>
            value.Name == "Plain" &&
            value.Type == "Integer" &&
            value.AbsoluteTarget is null));
}

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2ScnMain.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real scene preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var realUnit = new DelphiParser(tokens).ParseUnit();
    var realFunction = realUnit.Declarations.OfType<DelphiFunction>()
        .Single(value => value.Name.Equals(
            "CompareDecorSprites", StringComparison.OrdinalIgnoreCase));
    foreach (var variable in realFunction.LocalVariables)
        Console.WriteLine(
            $"{variable.Name}: type={variable.Type}; absolute={variable.AbsoluteTarget}");

    foreach (var pair in new[]
    {
        (Alias: "l_DS1", Target: "Item1"),
        (Alias: "l_DS2", Target: "Item2")
    })
    {
        Check("Real AST " + pair.Alias,
            realFunction.LocalVariables.Any(value =>
                value.Name == pair.Alias &&
                value.Type == "Tf2DecorationSprite" &&
                value.AbsoluteTarget == pair.Target));

        foreach (var entry in new[]
        {
            (Name: "Builder", Model: SemanticModelBuilder.Build(realUnit)),
            (Name: "Analyzer", Model: SemanticAnalyzer.Analyze(realUnit))
        })
        {
            Check("Real " + entry.Name + " " + pair.Alias,
                entry.Model.Functions.Any(value =>
                    value.Name.Equals("CompareDecorSprites",
                        StringComparison.OrdinalIgnoreCase) &&
                    value.LocalVariables.Any(variable =>
                        variable.Name == pair.Alias &&
                        variable.Type == "Tf2DecorationSprite" &&
                        variable.AbsoluteTarget == pair.Target)));
        }
    }
}

Console.WriteLine($"Absolute-metadata checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;