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
    Console.WriteLine("Usage:");
    Console.WriteLine(
        "  FireURQ.Porter <file.pas>");
    Console.WriteLine(
        "  FireURQ.Porter --analyze <directory>");
    Console.WriteLine();

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

    BatchAnalyzer.Analyze(args[1]);

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

DelphiAstPrinter.Print(unit);