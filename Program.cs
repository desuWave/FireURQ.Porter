using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.Translation;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

Encoding.RegisterProvider(
    CodePagesEncodingProvider.Instance);

Console.WriteLine("================================");
Console.WriteLine(" FireURQ.Porter");
Console.WriteLine(" Delphi Source Parser");
Console.WriteLine("================================");
Console.WriteLine();

if (args.Length == 0)
{
    PrintUsage();
    return;
}

// ========================================================
// BATCH ANALYSIS
// ========================================================

if (args[0].Equals(
        "--analyze",
        StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.WriteLine(
            "ERROR: Analysis directory is missing.");

        Console.WriteLine();

        return;
    }

    if (args.Length >= 2 &&
        args[0].Equals(
            "--trace-headers",
            StringComparison.OrdinalIgnoreCase))
    {
        ImplementationHeaderTrace.Analyze(args[1]);
        return;
    }

    if (args.Length >= 2 &&
        args[0].Equals(
            "--probe-expressions",
            StringComparison.OrdinalIgnoreCase))
    {
        ExpressionProbe.Analyze(args[1]);
        return;
    }

var rootPaths =
    args
        .Skip(1)
        .TakeWhile(
            arg =>
                !arg.Equals(
                    "--export-graph",
                    StringComparison.OrdinalIgnoreCase))
        .ToList();

if (rootPaths.Count == 0)
{
    Console.WriteLine(
        "ERROR: Analysis directory is missing.");

    Console.WriteLine();

    return;
}

string? exportPath =
    GetOptionValue(
        args,
        "--export-graph");

ProjectDependencyGraph? projectGraph =
    BatchAnalyzer.Analyze(
        rootPaths);

    if (projectGraph != null &&
    !string.IsNullOrWhiteSpace(exportPath))
    {
        Console.WriteLine(
            "================================");

        Console.WriteLine(
            " Exporting Dependency Graph");

        Console.WriteLine(
            "================================");

        DependencyGraphJsonExporter.Export(
            projectGraph,
            exportPath);

        Console.WriteLine();

        Console.WriteLine(
            $"Graph exported to:");

        Console.WriteLine(
            Path.GetFullPath(exportPath));

        Console.WriteLine();

        Console.WriteLine(
            $"Nodes: {projectGraph.Units.Count}");

        Console.WriteLine(
            $"Edges: {projectGraph.Edges.Count}");

        Console.WriteLine();
    }

    return;
}

if (args.Length >= 1 &&
    args[0].Equals(
        "--test-paging",
        StringComparison.OrdinalIgnoreCase))
{
    var lines =
        Enumerable
            .Range(1, 100)
            .Select(
                x => $"Diagnostic line {x}")
            .ToList();

    var listing =
        new PagedListing(
            lines,
            pageSize: 10);

    PagedListingViewer.Show(
        listing,
        "FireURQ.Porter — Paging Test");

    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--probe-expressions",
        StringComparison.OrdinalIgnoreCase))
{
    ExpressionProbe.Analyze(args[1]);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals("--analyze-bodies", StringComparison.OrdinalIgnoreCase))
{
    bool showProgress =
        args.Any(
    x => x.Equals(
        "--progress",
        StringComparison.OrdinalIgnoreCase));

    BodyBatchAnalyzer.Analyze(args[1], showProgress);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--audit-methods",
        StringComparison.OrdinalIgnoreCase))
{
    MethodResolutionAudit.Analyze(args[1]);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--census-methods",
        StringComparison.OrdinalIgnoreCase))
{
    MethodImplementationCensus.Analyze(args[1]);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--census-headers",
        StringComparison.OrdinalIgnoreCase))
{
    ImplementationHeaderCensus.Analyze(args[1]);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--trace-headers",
        StringComparison.OrdinalIgnoreCase))
{
    ImplementationHeaderTrace.Analyze(args[1]);
    return;
}

if (args.Length >= 2 &&
    args[0].Equals(
        "--browse-bodies",
        StringComparison.OrdinalIgnoreCase))
{
    BodyIndex index =
        BodyIndex.Build(args[1]);

    BodyBrowser.Show(index);

    return;
}

if (args.Length >= 3 &&
    args[0].Equals(
        "--probe-body-owner",
        StringComparison.OrdinalIgnoreCase))
{
    BodyOwnerProbe.Analyze(
        args[1],
        args[2]);

    return;
}

// ========================================================
// TRANSLATION TEST
// ========================================================

if (args.Length >= 1 &&
    args[0].Equals(
        "--translate-test",
        StringComparison.OrdinalIgnoreCase))
{
    string testFile =
        Path.Combine(
            "tests",
            "TranslationTest.pas");

    if (!File.Exists(testFile))
    {
        Console.WriteLine(
            $"ERROR: Translation test file not found: {testFile}");

        return;
    }

    Console.WriteLine(
        $"Translation source: {testFile}");

    Console.WriteLine();

    string testSource =
        File.ReadAllText(
            testFile,
            Encoding.UTF8);

    var testLexer =
        new DelphiLexer(testSource);

    var testTokens =
        testLexer.Tokenize();

    var testParser =
        new DelphiParser(testTokens);

    DelphiUnit testUnit =
        testParser.ParseUnit();

    SemanticUnit semanticUnit =
        SemanticAnalyzer.Analyze(testUnit);

    IReadOnlyList<BodyTokenAnalyzer.BodyInfo> bodies =
        BodyTokenAnalyzer.Analyze(
            testUnit,
            testFile);

var translator =
    new DelphiToCSharpTranslator();

var declarationTranslator =
    new CSharpDeclarationTranslator();

Console.WriteLine(
    "================================");

Console.WriteLine(
    " T0 Translation Test");

Console.WriteLine(
    "================================");

Console.WriteLine();

int translatedCount = 0;

Console.WriteLine(
    "================================");

Console.WriteLine(
    " T2 Declaration Translation");

Console.WriteLine(
    "================================");

Console.WriteLine();

string declarations =
    declarationTranslator.Translate(
        semanticUnit);

Console.WriteLine(
    declarations);

foreach (BodyTokenAnalyzer.BodyInfo body in bodies)
{
    if (body.BodyTokens.Count == 0)
        continue;

SemanticFunction? function =
    FunctionImplementationMatcher.Match(
        testUnit,
        body,
        semanticUnit);

if (function is null)
{
    Console.WriteLine(
        $"SKIP: No matching implementation for {body.Name}");

    continue;
}

    var behavior =
        BehaviorAnalyzer.Analyze(
            body.FilePath,
            body.Name,
            body.BodyTokens);

    string csharp =
        translator.TranslateFunction(
            function,
            behavior);

    Console.WriteLine(
        $"// Delphi: {body.Name}");

    Console.WriteLine(
        csharp);

    Console.WriteLine();

    translatedCount++;
}

Console.WriteLine(
    $"Translated bodies: {translatedCount}");

return;
}

// ========================================================
// SINGLE FILE
// ========================================================

string filePath = args[0];

if (!File.Exists(filePath))
{
    Console.WriteLine(
        "ERROR: File not found:");

    Console.WriteLine(filePath);

    return;
}

string? singleFileExportPath =
    GetOptionValue(
        args,
        "--export-graph");

Console.WriteLine(
    $"Source: {filePath}");

Console.WriteLine();

string source =
    File.ReadAllText(
        filePath,
        Encoding.GetEncoding(1251));

var lexer =
    new DelphiLexer(source);

var tokens =
    lexer.Tokenize();

Console.WriteLine(
    $"Lexer tokens: {tokens.Count}");

Console.WriteLine();

var parser =
    new DelphiParser(tokens);

DelphiUnit unit =
    parser.ParseUnit();

DelphiAstPrinter.Print(
    unit);

var semanticModel =
    SemanticModelBuilder.Build(
        unit);

SemanticPrinter.Print(
    semanticModel);

var dependencyGraph =
    DependencyGraphBuilder.Build(
        semanticModel);

DependencyGraphPrinter.Print(
    dependencyGraph);

if (!string.IsNullOrWhiteSpace(
        singleFileExportPath))
{
    Console.WriteLine(
        "================================");

    Console.WriteLine(
        " Exporting Dependency Graph");

    Console.WriteLine(
        "================================");

    DependencyGraphJsonExporter.Export(
        dependencyGraph,
        singleFileExportPath);

    Console.WriteLine();

    Console.WriteLine(
        $"Graph exported to:");

    Console.WriteLine(
        Path.GetFullPath(
            singleFileExportPath));

    Console.WriteLine();

    Console.WriteLine(
        $"Nodes: {dependencyGraph.Nodes.Count}");

    Console.WriteLine(
        $"Edges: {dependencyGraph.Edges.Count}");

    Console.WriteLine();
}

// ========================================================
// HELPERS
// ========================================================

static string? GetOptionValue(
    string[] args,
    string option)
{
    for (int i = 0;
         i < args.Length - 1;
         i++)
    {
        if (args[i].Equals(
                option,
                StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine(
        "  FireURQ.Porter --translate-test");

    Console.WriteLine(
        "  FireURQ.Porter --probe-body-owner <directory> <body>");

    Console.WriteLine(
        "  FireURQ.Porter --browse-bodies <directory>");

    Console.WriteLine(
        "  FireURQ.Porter --probe-expressions <directory>");

    Console.WriteLine(
        "  FireURQ.Porter --trace-headers <directory>");

    Console.WriteLine(
        "  FireURQ.Porter --census-headers <directory>");

    Console.WriteLine(
        "  FireURQ.Porter --census-methods <directory>");

    Console.WriteLine(
    "  FireURQ.Porter --audit-methods <directory>");

    Console.WriteLine("Usage:");

    Console.WriteLine(
        "  FireURQ.Porter <file.pas>");

    Console.WriteLine(
        "  FireURQ.Porter <file.pas> " +
        "--export-graph <file.json>");

    Console.WriteLine(
        "  FireURQ.Porter --analyze <directory>");

    Console.WriteLine(
        "  FireURQ.Porter --analyze <directory> " +
        "--export-graph <file.json>");

    Console.WriteLine();
}
