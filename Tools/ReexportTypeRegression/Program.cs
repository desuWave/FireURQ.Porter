using System.Text;
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
SemanticUnit Parse(string source) =>
    SemanticModelBuilder.Build(new DelphiParser(
        new DelphiLexer(source).Tokenize()).ParseUnit());

var origin = Parse(
    "unit Origin; interface type THandle = Cardinal; implementation end.");
var facade = Parse(
    "unit Facade; interface uses Origin; type THandle = Origin.THandle; implementation end.");
var consumer = Parse(
    "unit Consumer; interface uses Origin, Facade; implementation end.");

foreach (var order in new[]
{
    new[] { origin, facade, consumer },
    new[] { consumer, facade, origin }
})
{
    var resolver = new TypeResolver(order);
    var result = resolver.Resolve("THandle", "Consumer");
    Check("Shared origin resolves regardless of unit order",
        result.IsResolved &&
        CSharpTypeMapper.Map("THandle", resolver, "Consumer") == "uint");
}

var independent = Parse(
    "unit Independent; interface type THandle = Cardinal; implementation end.");
var conflict = Parse(
    "unit Conflict; interface uses Origin, Independent; implementation end.");
Check("Independent aliases to same primitive remain ambiguous",
    new TypeResolver(new[] { origin, independent, conflict })
        .Resolve("THandle", "Conflict").IsAmbiguous);

var different = Parse(
    "unit Different; interface type THandle = Boolean; implementation end.");
var mismatch = Parse(
    "unit Mismatch; interface uses Origin, Different; implementation end.");
Check("Different imported types remain ambiguous",
    new TypeResolver(new[] { origin, different, mismatch })
        .Resolve("THandle", "Mismatch").IsAmbiguous);

var local = Parse(
    "unit Local; interface uses Origin, Facade; type THandle = Boolean; implementation end.");
var localResolver = new TypeResolver(new[] { origin, facade, local });
Check("Local declaration still takes precedence",
    CSharpTypeMapper.Map("THandle", localResolver, "Local") == "bool");
Check("Qualified source remains available",
    CSharpTypeMapper.Map("Origin.THandle", localResolver, "Local") == "uint");

var cycle = Parse(
    "unit Cycle; interface type THandle = Other; Other = THandle; implementation end.");
var cycleConsumer = Parse(
    "unit CycleConsumer; interface uses Cycle, Origin; implementation end.");
Check("Cyclic candidate is not merged",
    new TypeResolver(new[] { cycle, origin, cycleConsumer })
        .Resolve("THandle", "CycleConsumer").IsAmbiguous);

var hidden = Parse(
    "unit Hidden; interface implementation end.");
Check("Unimported declarations remain unavailable",
    new TypeResolver(new[] { origin, facade, hidden })
        .Resolve("THandle", "Hidden").Chain.Count == 0);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var realUnits = new List<SemanticUnit>();
foreach (string file in new[] { "Dynamic_Bass.pas", "d2dTypes.pas", "d2dCore.pas" })
{
    string path = Path.Combine(
        @"E:\EVI_DATA\EVI_Projects\d2dge-furq_2_2_4", file);
    bool ok = new DelphiConditionalPreprocessor().TryProcess(
        new DelphiLexer(File.ReadAllText(
            path, Encoding.GetEncoding(1251))).Tokenize(),
        out var tokens, out var error, sourcePath: path);
    Check("Preprocess " + file, ok);
    if (!ok)
    {
        Console.WriteLine(error);
        continue;
    }
    realUnits.Add(SemanticModelBuilder.Build(
        new DelphiParser(tokens).ParseUnit()));
}

if (realUnits.Count == 3)
{
    foreach (var order in new[]
    {
        realUnits.ToArray(),
        realUnits.AsEnumerable().Reverse().ToArray()
    })
    {
        var resolver = new TypeResolver(order);
        foreach (string name in new[] { "HSAMPLE", "HCHANNEL", "HSTREAM", "HMUSIC" })
        {
            var resolution = resolver.Resolve(name, "d2dCore");
            bool passed = resolution.IsResolved &&
                CSharpTypeMapper.Map(name, resolver, "d2dCore") == "uint";
            Check("Real d2dCore " + name + " resolves", passed);
        }
    }
}

Console.WriteLine($"Re-export checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;