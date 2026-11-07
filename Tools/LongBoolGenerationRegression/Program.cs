using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

string source = """
unit LongBoolTest;
interface
type
  BOOL = LongBool;
  TNative = function(value: BOOL; var changed: BOOL): BOOL; stdcall;
  TManaged = function(value: BOOL): BOOL of object;
  TRecord = record
    Flag: BOOL;
    Next: Integer;
  end;
var
  NativeCall: function(value: BOOL): BOOL; stdcall;
implementation
end.
""";

var model = SemanticModelBuilder.Build(
    new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit());

var resolver = new TypeResolver(new[] { model });
var translator = new CSharpDeclarationTranslator(resolver, model.Name);

string generated = translator.Translate(model);
Console.WriteLine(generated);

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("LongBool maps to bool",
    CSharpTypeMapper.Map("LongBool") == "bool");

Check("BOOL alias maps to bool",
    CSharpTypeMapper.Map("BOOL", resolver, model.Name) == "bool");

Check("Named native delegate parameters",
    generated.Contains(
        "TNative([MarshalAs(UnmanagedType.Bool)] bool @value, " +
        "[MarshalAs(UnmanagedType.Bool)] ref bool @changed)"));

Check("Procedural-variable delegate parameter",
    generated.Contains(
        "NativeCallDelegate([MarshalAs(UnmanagedType.Bool)] bool @value)"));

Check("Both native returns have explicit BOOL marshalling",
    System.Text.RegularExpressions.Regex.Matches(
        generated,
        System.Text.RegularExpressions.Regex.Escape(
            "[return: MarshalAs(UnmanagedType.Bool)]")).Count == 2);

Check("Managed method delegate remains managed",
    generated.Contains("public delegate bool TManaged(bool @value);"));

Check("Record field has explicit BOOL marshalling",
    System.Text.RegularExpressions.Regex.IsMatch(
        generated,
        @"\[MarshalAs\(UnmanagedType.Bool\)\]\s+public bool Flag;"));

Check("Record following field remains int",
    generated.Contains("public int Next;"));

Console.WriteLine($"LongBool generation checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
