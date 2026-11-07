using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

bool ParametersAgree(SemanticFunction actual, DelphiFunction expected) =>
    actual.Parameters.Count == expected.Parameters.Count &&
    actual.Parameters.Zip(expected.Parameters).All(pair =>
        pair.First.Name.Equals(
            pair.Second.Name, StringComparison.OrdinalIgnoreCase) &&
        pair.First.Type.Equals(
            pair.Second.Type, StringComparison.OrdinalIgnoreCase) &&
        pair.First.Kind.Equals(
            pair.Second.Kind.ToString(), StringComparison.OrdinalIgnoreCase));

void CheckBodies(DelphiUnit unit, string path, Func<string, bool> select)
{
    var model = SemanticModelBuilder.Build(unit);
    var implementations = unit.Declarations.OfType<DelphiFunction>()
        .Where(f => f.BodyTokens.Count > 0).ToArray();

    foreach (var body in BodyTokenAnalyzer.Analyze(unit, path)
        .Where(b => b.BodyTokens.Count > 0 && select(b.Name)))
    {
        var expected = implementations.FirstOrDefault(f =>
            ReferenceEquals(f.BodyTokens, body.BodyTokens));

        var actual = FunctionImplementationMatcher.Match(unit, body, model);

        Check($"{Path.GetFileName(path)}: {body.Name} signature matches its body",
            expected is not null &&
            actual is not null &&
            ParametersAgree(actual, expected));

        var copied = body with { BodyTokens = body.BodyTokens.ToArray() };
        var copiedMatch = FunctionImplementationMatcher.Match(unit, copied, model);

        Check($"{body.Name}: copied token list matches same signature",
            expected is not null &&
            copiedMatch is not null &&
            ParametersAgree(copiedMatch, expected));

        if (actual is not null)
            Console.WriteLine("  Parameters: " + string.Join(", ",
                actual.Parameters.Select(p => $"{p.Kind} {p.Type} {p.Name}")));
    }
}

string source = """
unit OverloadBodyTest;
interface
type
  TExample = class
  public
    procedure Act(value: Integer); overload;
    procedure Act(value: String); overload;
  end;
function Echo(value: Integer): Integer; overload;
function Echo(value: Boolean): Boolean; overload;
implementation

procedure TExample.Act(value: Integer);
begin
  value := value;
end;

procedure TExample.Act(value: String);
begin
  value := value;
end;

function Echo(value: Integer): Integer;
begin
  Result := value;
end;

function Echo(value: Boolean): Boolean;
begin
  Result := value;
end;

end.
""";

var synthetic = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
CheckBodies(synthetic, "OverloadBodyTest.pas", _ => true);

var syntheticModel = SemanticModelBuilder.Build(synthetic);
var syntheticBodies = BodyTokenAnalyzer.Analyze(
    synthetic, "OverloadBodyTest.pas");

Check("All four synthetic bodies are present",
    syntheticBodies.Count(b => b.BodyTokens.Count > 0) == 4);

var firstBody = syntheticBodies.First(b => b.BodyTokens.Count > 0);
var missing = firstBody with { Name = "MissingImplementation" };

Check("Missing implementation returns null",
    FunctionImplementationMatcher.Match(
        synthetic, missing, syntheticModel) is null);

var realFiles = new[]
{
    @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dGUIMenus.pas",
    @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dStorage.pas",
    @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dUtils.pas",
    @"E:\EVI_DATA\EVI_Projects\fireurq-master\f2\f2TextPane.pas"
};

foreach (string path in realFiles)
{
    var processor = new DelphiConditionalPreprocessor();
    bool processed = processor.TryProcess(
        new DelphiLexer(File.ReadAllText(
            path, Encoding.GetEncoding(1251))).Tokenize(),
        out var tokens, out var error, sourcePath: path);

    Check($"Preprocess {Path.GetFileName(path)}", processed);
    if (!processed)
    {
        Console.WriteLine(error);
        continue;
    }

    var unit = new DelphiParser(tokens).ParseUnit();

    CheckBodies(unit, path, name => name is
        "Td2dMenu.Create" or "Td2dMenu.Popup" or
        "Td2dSetupStorage.Make" or "D2DIsPointInRect" or
        "Tf2TextPane.DoButtonAction");
}

Console.WriteLine($"Overload-body checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
