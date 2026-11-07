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

string source = """
unit Boundaries;
interface
implementation
procedure First;
begin
  try
    case 1 of
      1: begin
        try
          Value := 1;
        except
          Value := 2;
        end;
      end;
    end;
  finally
    Cleanup;
  end;
  AfterFinally;
end;
procedure Second;
begin
  case 2 of
    2: begin
      Value := 3;
    end;
  end;
  AfterCase;
end;
procedure Third;
begin
  Value := 4;
end;
end.
""";

var unit = new DelphiParser(new DelphiLexer(source).Tokenize()).ParseUnit();
var functions = unit.Declarations.OfType<DelphiFunction>().ToArray();
Check("All three implementations survive nested blocks", functions.Length == 3);

var first = functions.FirstOrDefault(value => value.Name == "First");
var second = functions.FirstOrDefault(value => value.Name == "Second");

Check("First body includes statement after finally",
    first is not null &&
    first.BodyTokens.Any(token => token.Text == "AfterFinally"));
Check("Second body includes statement after case",
    second is not null &&
    second.BodyTokens.Any(token => token.Text == "AfterCase"));
Check("Following routine does not enter first body",
    first is not null &&
    !first.BodyTokens.Any(token => token.Text == "Second"));
Check("Internal block semicolons are preserved",
    first is not null &&
    first.BodyTokens.Zip(first.BodyTokens.Skip(1)).Any(pair =>
        pair.First.Text.Equals("end", StringComparison.OrdinalIgnoreCase) &&
        pair.Second.Text == ";"));

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
string path = @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4\d2dZipPack.pas";
bool processed = new DelphiConditionalPreprocessor().TryProcess(
    new DelphiLexer(File.ReadAllText(
        path, Encoding.GetEncoding(1251))).Tokenize(),
    out var tokens, out var error, sourcePath: path);
Check("Real ZIP preprocessing", processed);

if (!processed)
    Console.WriteLine(error);
else
{
    var realUnit = new DelphiParser(tokens).ParseUnit();
    var model = SemanticModelBuilder.Build(realUnit);
    var bodies = BodyTokenAnalyzer.Analyze(realUnit, path)
        .Where(body => body.BodyTokens.Count > 0).ToArray();

    string[] expected =
    {
        "Td2dZipPack.Create",
        "Td2dZipPack.Find",
        "Td2dZipPack.LoadFromStream",
        "Td2dZipPack.LoadFromFile",
        "Td2dZipPack.Extract",
        "Td2dZipPack.pm_GetCount",
        "Td2dZipPack.pm_GetName",
        "Td2dZipPack.IndexOf",
        "Td2dZipPack.Make",
        "Td2dZipPack.pm_GetSize",
        "ZipCRC32"
    };

    Console.WriteLine("=== Real ZIP body boundaries ===");
    foreach (var body in bodies)
        Console.WriteLine(
            $"{body.Name}: {body.BodyTokens[0].Line}..{body.BodyTokens[^1].Line}");

    Check("Exactly eleven real ZIP implementations", bodies.Length == 11);
    foreach (string name in expected)
    {
        var matches = bodies.Where(body => body.Name.Equals(
            name, StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(name + ": one body and matching signature",
            matches.Length == 1 &&
            FunctionImplementationMatcher.Match(
                realUnit, matches[0], model) is not null);
    }

    var find = bodies.FirstOrDefault(body =>
        body.Name == "Td2dZipPack.Find");
    Check("Find ends at its own final end on line 125",
        find is not null && find.BodyTokens[^1].Line == 125);
}

Console.WriteLine($"Body-boundary checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;