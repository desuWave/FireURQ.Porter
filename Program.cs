using System.Text;
using FireURQ.Porter.AST;
using FireURQ.Porter.Analysis;
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

    string rootPath = args[1];

    string? exportPath =
        GetOptionValue(
            args,
            "--export-graph");

    ProjectDependencyGraph? projectGraph =
    BatchAnalyzer.Analyze(
        rootPath);

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
