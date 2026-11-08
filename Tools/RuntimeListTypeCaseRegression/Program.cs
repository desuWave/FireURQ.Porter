using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool condition)
{
    total++;
    if (!condition)
        failed++;
    Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {name}");
}

var standardUnit = new DelphiParser(new DelphiLexer("""
unit StandardListCase;
interface
type
  TPointerListAlias = Tlist;
  TObjectListAlias = tobjectlist;
  TStringListAlias = ijclstringlist;
implementation
end.
""").Tokenize()).ParseUnit();
var standardModel = SemanticModelBuilder.Build(standardUnit);
var standardResolver = new TypeResolver(new[] { standardModel });

foreach (var sample in new[]
{
    (Input: "Tlist", Expected: "TList"),
    (Input: "TLIST", Expected: "TList"),
    (Input: "tLiSt", Expected: "TList"),
    (Input: "tobjectlist", Expected: "TObjectList"),
    (Input: "TOBJECTLIST", Expected: "TObjectList"),
    (Input: "ijclstringlist", Expected: "IJclStringList")
})
{
    Check(sample.Input + ": context-free canonical name",
        CSharpTypeMapper.Map(sample.Input) == sample.Expected);
    Check(sample.Input + ": contextual canonical name",
        CSharpTypeMapper.Map(
            sample.Input, standardResolver, standardModel.Name) == sample.Expected);
}

Check("Alias to mixed-case TList maps canonically",
    CSharpTypeMapper.Map("TPointerListAlias",
        standardResolver, standardModel.Name) == "TList");
Check("Alias to mixed-case TObjectList maps canonically",
    CSharpTypeMapper.Map("TObjectListAlias",
        standardResolver, standardModel.Name) == "TObjectList");
Check("Alias to mixed-case IJclStringList maps canonically",
    CSharpTypeMapper.Map("TStringListAlias",
        standardResolver, standardModel.Name) == "IJclStringList");
Check("Array element names are normalized recursively",
    CSharpTypeMapper.Map("array of Tlist",
        standardResolver, standardModel.Name) == "TList[]");

var customUnit = new DelphiParser(new DelphiLexer("""
unit CustomListCase;
interface
type
  Tlist = class
  end;
  TObjectlist = class
  end;
  Ijclstringlist = interface
  end;
implementation
end.
""").Tokenize()).ParseUnit();
var customModel = SemanticModelBuilder.Build(customUnit);
var customResolver = new TypeResolver(new[] { customModel });

Check("Project-defined Tlist retains its declared spelling",
    CSharpTypeMapper.Map("TLIST",
        customResolver, customModel.Name) == "Tlist");
Check("Project-defined TObjectlist retains its declared spelling",
    CSharpTypeMapper.Map("TOBJECTLIST",
        customResolver, customModel.Name) == "TObjectlist");
Check("Project-defined interface retains its declared spelling",
    CSharpTypeMapper.Map("IJCLSTRINGLIST",
        customResolver, customModel.Name) == "Ijclstringlist");

Check("Unknown type name remains unchanged",
    CSharpTypeMapper.Map("TUnknownList",
        standardResolver, standardModel.Name) == "TUnknownList");

Console.WriteLine($"Runtime-list-type-case checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;