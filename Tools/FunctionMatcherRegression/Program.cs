using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

var filePath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "ForwardDeclarationTest.pas"));

Console.WriteLine($"Test file: {filePath}");

if (!File.Exists(filePath))
{
    Console.Error.WriteLine("ERROR: Pascal test file not found.");
    return 2;
}

var source = File.ReadAllText(filePath);
var lexer = new DelphiLexer(source);
var tokens = lexer.Tokenize();
var parser = new DelphiParser(tokens);
var unit = parser.ParseUnit();

var semanticUnit = SemanticAnalyzer.Analyze(unit);
var bodies = BodyTokenAnalyzer.Analyze(unit, filePath);

var methodBodies = bodies
    .Where(body =>
        body.OwnerKind == BodyTokenAnalyzer.BodyOwnerKind.Class &&
        body.Name.Equals(
            "TExample.GetValue",
            StringComparison.OrdinalIgnoreCase) &&
        body.TokenCount > 0)
    .ToList();

Console.WriteLine($"Class method bodies found: {methodBodies.Count}");

foreach (var body in methodBodies)
{
    Console.WriteLine(
        $"BODY: Name={body.Name}; OwnerKind={body.OwnerKind}; Tokens={body.TokenCount}");
}

if (methodBodies.Count != 1)
{
    Console.Error.WriteLine(
        "FAIL: Expected exactly one TExample.GetValue body.");
    return 1;
}

var match = FunctionImplementationMatcher.Match(
    unit,
    methodBodies[0],
    semanticUnit);

if (match is null)
{
    Console.Error.WriteLine(
        "FAIL: Forward-declared class method was not matched.");
    return 1;
}

Console.WriteLine($"Matched method: {match.Name}");
Console.WriteLine("PASS: Forward-declaration regression test.");
return 0;
