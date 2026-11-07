using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;
using FireURQ.Porter.Translation;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var source = new SemanticUnit(
    "Source",
    Array.Empty<string>(),
    new[] { new SemanticClass("TFURQContext", null, Array.Empty<SemanticMember>()) },
    new[] { new SemanticInterface("IFURQContext", null, Array.Empty<SemanticMember>()) },
    Array.Empty<SemanticType>(),
    Array.Empty<SemanticConstant>(),
    Array.Empty<SemanticVariable>(),
    Array.Empty<SemanticFunction>());

var consumer = new SemanticUnit(
    "Consumer",
    new[] { "Source" },
    Array.Empty<SemanticClass>(),
    Array.Empty<SemanticInterface>(),
    new[] { new SemanticType("TContextAlias", "Simple", "TfurqContext") },
    Array.Empty<SemanticConstant>(),
    Array.Empty<SemanticVariable>(),
    Array.Empty<SemanticFunction>());

var resolver = new TypeResolver(new[] { source, consumer });

int total = 0;
int failed = 0;

void Check(string name, bool passed)
{
    total++;
    if (!passed) failed++;
    Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}");
}

Check("Imported class spelling",
    CSharpTypeMapper.Map("TfurqContext", resolver, "Consumer") == "TFURQContext");
Check("Imported interface spelling",
    CSharpTypeMapper.Map("IfurqContext", resolver, "Consumer") == "IFURQContext");
Check("Alias to imported class",
    CSharpTypeMapper.Map("TContextAlias", resolver, "Consumer") == "TFURQContext");
Check("Qualified class spelling",
    CSharpTypeMapper.Map("source . tfurqcontext", resolver, "Consumer") == "TFURQContext");
Check("Unknown external class is preserved",
    CSharpTypeMapper.Map("TExternal", resolver, "Consumer") == "TExternal");

string root = @"E:\EVI_DATA\EVI_Projects\fireurq-master";
var models = new List<SemanticUnit>();

foreach (string name in new[] { "furqContext.pas", "furqExprEval.pas" })
{
    string path = Path.Combine(root, name);
    var processor = new DelphiConditionalPreprocessor();

    bool processed = processor.TryProcess(
        new DelphiLexer(File.ReadAllText(
            path, Encoding.GetEncoding(1251))).Tokenize(),
        out var tokens, out var error, sourcePath: path);

    Check($"Preprocess {name}", processed);
    if (!processed)
    {
        Console.WriteLine(error);
        continue;
    }

    models.Add(SemanticModelBuilder.Build(
        new DelphiParser(tokens).ParseUnit()));
}

if (models.Count == 2)
{
    var projectResolver = new TypeResolver(models);
    var evaluator = models.Single(model =>
        model.Name.Equals("furqExprEval", StringComparison.OrdinalIgnoreCase));

    string generated = new CSharpDeclarationTranslator(
        projectResolver, evaluator.Name).Translate(evaluator);

    Check("Real evaluator field uses declared class spelling",
        generated.Contains("TFURQContext f_Context;"));

    Check("Real evaluator field no longer uses mismatched spelling",
        !generated.Contains("TfurqContext f_Context;"));
}

Console.WriteLine($"Class type case checks: {total - failed}/{total} passed.");
return failed == 0 ? 0 : 1;
