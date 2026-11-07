using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

string source = """
unit BooleanAliasTest;
interface
type
  TFlag = LongBool;
implementation

function AliasNot(value: TFlag): Boolean;
begin
  Result := not value;
end;

function AliasAnd(a, b: TFlag): Boolean;
begin
  Result := a and b;
end;

function AliasOr(a, b: TFlag): Boolean;
begin
  Result := a or b;
end;

function IntegerNot(value: Integer): Integer;
begin
  Result := not value;
end;

function IntegerAnd(a, b: Integer): Integer;
begin
  Result := a and b;
end;

end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var model = SemanticModelBuilder.Build(unit);
var resolver = new TypeResolver(new[] { model });
var translator = new DelphiToCSharpTranslator(resolver, model.Name);
var translated = new Dictionary<string, string>();

foreach (var body in BodyTokenAnalyzer.Analyze(unit, "BooleanAliasTest.pas"))
{
    if (body.BodyTokens.Count == 0)
        continue;

    var function = FunctionImplementationMatcher.Match(unit, body, model);
    if (function is null)
        continue;

    var behavior = BehaviorAnalyzer.Analyze(
        body.FilePath, body.Name, body.BodyTokens);

    translated[function.Name] =
        translator.TranslateFunction(function, behavior);
}

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

bool Contains(string name, string expected) =>
    translated.TryGetValue(name, out string? generated) &&
    generated.Contains(expected);

Check("All five function bodies translated", translated.Count == 5);
Check("Alias NOT uses logical negation", Contains("AliasNot", "!value"));
Check("Alias AND uses logical operator", Contains("AliasAnd", "a && b"));
Check("Alias OR uses logical operator", Contains("AliasOr", "a || b"));
Check("Integer NOT stays bitwise", Contains("IntegerNot", "~value"));
Check("Integer AND stays bitwise",
    Contains("IntegerAnd", "a & b") &&
    !Contains("IntegerAnd", "&&"));

foreach (var entry in translated)
{
    Console.WriteLine($"=== {entry.Key} ===");
    Console.WriteLine(entry.Value);
}

Console.WriteLine($"Boolean-alias checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
