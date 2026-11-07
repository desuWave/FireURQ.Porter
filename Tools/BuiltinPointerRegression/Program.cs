using FireURQ.Porter.Analysis;
using FireURQ.Porter.Translation;

static SemanticUnit Unit(string name, params SemanticType[] types) =>
    new(name,
        Array.Empty<string>(),
        Array.Empty<SemanticClass>(),
        Array.Empty<SemanticInterface>(),
        types,
        Array.Empty<SemanticConstant>(),
        Array.Empty<SemanticVariable>(),
        Array.Empty<SemanticFunction>());

var standard = Unit("Standard",
    new SemanticType("TAddress", "Simple", "Pointer"),
    new SemanticType("PAddress", "Pointer", "TAddress"));

var shadow = Unit("Shadow",
    new SemanticType("PChar", "Simple", "Integer"));

var resolver = new TypeResolver(new[] { standard, shadow });

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

foreach (string name in new[]
{
    "Pointer", "PChar", "PAnsiChar", "PWideChar",
    "PByte", "PLongWord", "PGUID"
})
{
    Check($"{name}: resolver recognizes pointer",
        resolver.TryResolvePointer(name, "Standard", out var resolution) &&
        resolution.PointerDepth == 1);

    Check($"{name}: contextual mapper uses IntPtr",
        CSharpTypeMapper.Map(name, resolver, "Standard") == "IntPtr");

    Check($"{name}: legacy mapper uses IntPtr",
        CSharpTypeMapper.Map(name) == "IntPtr");
}

Check("Alias to standard pointer",
    resolver.TryResolvePointer("TAddress", "Standard", out var alias) &&
    alias.PointerDepth == 1 &&
    CSharpTypeMapper.Map("TAddress", resolver, "Standard") == "IntPtr");

Check("Pointer to standard pointer has depth two",
    resolver.Resolve("PAddress", "Standard").PointerDepth == 2);

Check("Explicit declaration takes precedence",
    CSharpTypeMapper.Map("PChar", resolver, "Shadow") == "int" &&
    !resolver.TryResolvePointer("PChar", "Shadow", out _));

Check("Case-insensitive standard lookup",
    CSharpTypeMapper.Map("pAnSiChAr", resolver, "Standard") == "IntPtr");

Check("Array of standard pointers",
    CSharpTypeMapper.Map(
        "array of PByte", resolver, "Standard") == "IntPtr[]");

Check("Unknown P-prefixed name is preserved",
    CSharpTypeMapper.Map(
        "PUnknownApplicationType", resolver, "Standard") ==
        "PUnknownApplicationType");

Console.WriteLine($"Built-in pointer checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
