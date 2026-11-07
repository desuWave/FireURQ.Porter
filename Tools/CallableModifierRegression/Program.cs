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
unit ModifierTest;
interface
type
TBase = class
  procedure Work; virtual;
  class function Make: Integer; virtual;
end;
TChild = class(TBase)
  procedure Work; override;
  procedure Later; overload; virtual;
  procedure AbstractWork; virtual; abstract;
  class function Make: Integer; override;
  class procedure StaticWork; static;
end;
implementation
procedure TBase.Work;
begin
end;
class function TBase.Make: Integer;
begin
  Result := 1;
end;
procedure TChild.Work;
begin
  inherited Work;
end;
procedure TChild.Later;
begin
end;
class function TChild.Make: Integer;
begin
  Result := 2;
end;
class procedure TChild.StaticWork;
begin
end;
end.
""").Tokenize()).ParseUnit();

var baseClass = unit.Declarations.OfType<DelphiClass>()
    .Single(value => value.Name == "TBase");
var child = unit.Declarations.OfType<DelphiClass>()
    .Single(value => value.Name == "TChild");

DelphiFunction Method(DelphiClass owner, string name) =>
    owner.Members.Select(value => value.Node).OfType<DelphiFunction>()
        .Single(value => value.Name == name);

Check("Virtual modifier preserved",
    Method(baseClass, "Work").Modifiers.Contains("virtual"));
Check("Override modifier preserved",
    Method(child, "Work").Modifiers.Contains("override"));
Check("Multiple modifiers preserved",
    Method(child, "Later").Modifiers.SequenceEqual(new[] { "overload", "virtual" }));
Check("Abstract modifier preserved",
    Method(child, "AbstractWork").Modifiers.Contains("abstract"));
Check("Class function declaration marked",
    Method(baseClass, "Make").IsClassMethod);
Check("Class override declaration marked",
    Method(child, "Make").IsClassMethod &&
    Method(child, "Make").Modifiers.Contains("override"));
Check("Static class procedure marked",
    Method(child, "StaticWork").IsClassMethod &&
    Method(child, "StaticWork").Modifiers.Contains("static"));
Check("Ordinary method remains an instance method",
    !Method(child, "Work").IsClassMethod);

var implementations = unit.Declarations.OfType<DelphiFunction>()
    .Where(value => value.BodyTokens.Count > 0).ToArray();
Check("All six implementation bodies preserved", implementations.Length == 6);
Check("Class implementations retain class marker",
    implementations.Count(value => value.IsClassMethod) == 3);
Check("Implementation body is not consumed as modifiers",
    implementations.Single(value => value.Name == "TChild.Make")
        .BodyTokens.Any(token => token.Text == "2"));

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
    var owner = real.Declarations.OfType<DelphiClass>().Single(value =>
        value.Name == "Td2dProtoObjectPrim");
    Check("Real Cleanup is virtual",
        Method(owner, "Cleanup").Modifiers.Contains("virtual"));
    Check("Real FreeInstance overrides base",
        Method(owner, "FreeInstance").Modifiers.Contains("override"));
    Check("Real NewInstance is a class override",
        Method(owner, "NewInstance").IsClassMethod &&
        Method(owner, "NewInstance").Modifiers.Contains("override"));
    Check("Real QueryInterface convention retained",
        Method(owner, "QueryInterface").Modifiers.Contains("stdcall"));
    Check("Real NewInstance implementation marked as class method",
        real.Declarations.OfType<DelphiFunction>().Any(value =>
            value.Name == "Td2dProtoObjectPrim.NewInstance" &&
            value.IsClassMethod && value.BodyTokens.Count > 0));
}

Console.WriteLine($"Callable-modifier checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;