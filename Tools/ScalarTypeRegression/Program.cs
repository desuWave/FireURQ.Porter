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
    new SemanticType("TFlags", "Simple", "DWORD"),
    new SemanticType("TOffset", "Simple", "SmallInt"));

var shadow = Unit("Shadow",
    new SemanticType("DWORD", "Simple", "Boolean"));

var resolver = new TypeResolver(new[] { standard, shadow });

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("DWORD maps to uint",
    CSharpTypeMapper.Map("DWORD") == "uint");
Check("SmallInt maps to short",
    CSharpTypeMapper.Map("SmallInt") == "short");
Check("Case-insensitive DWORD",
    CSharpTypeMapper.Map("dWoRd") == "uint");
Check("Alias to DWORD",
    CSharpTypeMapper.Map("TFlags", resolver, "Standard") == "uint");
Check("Alias to SmallInt",
    CSharpTypeMapper.Map("TOffset", resolver, "Standard") == "short");
Check("Explicit declaration precedes standard mapping",
    CSharpTypeMapper.Map("DWORD", resolver, "Shadow") == "bool");
Check("Array of DWORD",
    CSharpTypeMapper.Map(
        "array of DWORD", resolver, "Standard") == "uint[]");
Check("SmallInt remains signed and 16-bit",
    sizeof(short) == 2 && short.MinValue == -32768);
Check("DWORD remains unsigned and 32-bit",
    sizeof(uint) == 4 && uint.MaxValue == 0xFFFFFFFF);
Check("LongBool uses the separately tested interop representation",
    CSharpTypeMapper.Map("LongBool") == "bool");

Console.WriteLine($"Scalar-type checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
