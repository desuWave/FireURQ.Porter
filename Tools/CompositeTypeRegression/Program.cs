using FireURQ.Porter.Analysis;
using FireURQ.Porter.Translation;

static SemanticUnit Unit(
    string name, string[] uses, params SemanticType[] types) =>
    new(name, uses,
        Array.Empty<SemanticClass>(),
        Array.Empty<SemanticInterface>(),
        types,
        Array.Empty<SemanticConstant>(),
        Array.Empty<SemanticVariable>(),
        Array.Empty<SemanticFunction>());

var source = Unit("Source", Array.Empty<string>(),
    new SemanticType("TElement", "Simple", "Byte"),
    new SemanticType("TFlags", "Set", "set of TElement"),
    new SemanticType("TItems", "Array", "array [0..3] of TElement"),
    new SemanticType("TSharedFlags", "Simple", "TFlags"),
    new SemanticType("TSharedItems", "Simple", "TItems"),
    new SemanticType("TNative", "Simple", "Cardinal"));

var consumer = Unit("Consumer", new[] { "Source" },
    new SemanticType("TElement", "Simple", "Integer"),
    new SemanticType("TFlags", "Set", "set of TElement"),
    new SemanticType("TItems", "Array", "array of TElement"),
    new SemanticType("TImportedId", "Simple", "Source . TNative"));

var resolver = new TypeResolver(new[] { source, consumer });

int total = 0;
int failed = 0;

void Check(string name, string input, string expected)
{
    total++;
    string actual = CSharpTypeMapper.Map(input, resolver, "Consumer");
    bool passed = actual == expected;
    if (!passed) failed++;

    Console.WriteLine(
        $"[{(passed ? "PASS" : "FAIL")}] {name}: {actual}");
}

Check("Imported alias expands in declaring module", "TSharedFlags", "HashSet<byte>");
Check("Local set takes precedence", "TFlags", "HashSet<int>");
Check("Imported array alias expands in declaring module", "TSharedItems", "byte[]");
Check("Local array takes precedence", "TItems", "int[]");
Check("Consumer local alias remains local", "TElement", "int");
Check("Qualified scalar", "Source.TNative", "uint");
Check("Spaced qualified scalar", "Source . TNative", "uint");
Check("Alias containing spaced qualified name", "TImportedId", "uint");

// Resolve returns the terminal name. Composite lookup must retain its owner.
Check("Qualified set expands", "Source.TFlags", "HashSet<byte>");
Check("Spaced qualified array expands", "Source . TItems", "byte[]");

Console.WriteLine($"Composite-type checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
