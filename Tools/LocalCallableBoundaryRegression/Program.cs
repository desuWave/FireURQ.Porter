using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

int total = 0;
int failed = 0;
void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

var unit = new DelphiParser(new DelphiLexer("""
unit LocalBoundaries;
interface
implementation
function Outer: Integer;
  function Inner(value: Integer; other: Integer): Integer;
  var local: Integer;
    procedure Nested;
    begin
      try
        case 1 of
          1: local := 1;
        end;
      finally
        local := 2;
      end;
    end;
  begin
    case value of
      1: Result := other;
    else
      Result := 0;
    end;
  end;
var afterLocal: Integer;
begin
  afterLocal := 3;
  Result := afterLocal;
end;
procedure Following;
begin
  Marker := 4;
end;
end.
""").Tokenize()).ParseUnit();

var functions = unit.Declarations.OfType<DelphiFunction>()
    .Where(value => value.BodyTokens.Count > 0).ToArray();
Check("Outer and following implementation survive", functions.Length == 2);
var outer = functions.FirstOrDefault(value => value.Name == "Outer");
Check("Outer body starts after local declarations",
    outer is not null &&
    outer.BodyTokens.Any(token => token.Text == "afterLocal") &&
    !outer.BodyTokens.Any(token => token.Text == "Nested"));
Check("Outer local after nested routine is preserved",
    outer is not null &&
    outer.LocalVariables.Any(value =>
        value.Name == "afterLocal" && value.Type == "Integer"));
Check("Following routine has its own body",
    functions.Any(value => value.Name == "Following" &&
        value.BodyTokens.Any(token => token.Text == "Marker")));

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\fireurq-master\furqContext.pas";
bool ok = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real context preprocessing", ok);
if (!ok)
    Console.WriteLine(error);
else
{
    var real = new DelphiParser(tokens).ParseUnit();
    var model = SemanticModelBuilder.Build(real);
    var bodies = BodyTokenAnalyzer.Analyze(real, path)
        .Where(value => value.BodyTokens.Count > 0).ToArray();

    Console.WriteLine("Real context implementation bodies: " + bodies.Length);

    foreach (string name in new[]
    {
        "LoadOperators",
        "TFURQCode.Create",
        "TFURQCode.Load",
        "TFURQContext.Create",
        "TFURQContext.Cleanup",
        "TFURQContext.DoLoadData",
        "TFURQContext.DoSaveData",
        "TFURQContext.GetVirtualVariable",
        "TFURQContext.pm_GetActionsData",
        "TFURQContext.SoundPlay",
        "TFURQContext.SoundStop",
        "TFURQCodePoint.pm_GetSrcFile"
    })
    {
        var found = bodies.Where(value =>
            value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(name + ": one complete body and matching signature",
            found.Length == 1 &&
            FunctionImplementationMatcher.Match(real, found[0], model) is not null);
        if (found.Length == 1)
            Console.WriteLine(
                $"  {found[0].BodyTokens[0].Line}..{found[0].BodyTokens[^1].Line}");
    }

    var load = bodies.FirstOrDefault(value => value.Name == "LoadOperators");
    Check("LoadOperators uses outer body at lines 394..407",
        load is not null &&
        load.BodyTokens[0].Line == 394 &&
        load.BodyTokens[^1].Line == 407);
}

Console.WriteLine($"Local-callable boundary checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;