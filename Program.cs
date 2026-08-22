using System.Text;
using FireURQ.Porter.AST;
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
    Console.WriteLine("  FireURQ.Porter <file.pas>");
    Console.WriteLine();
    return;
}

string filePath = args[0];

if (!File.Exists(filePath))
{
    Console.WriteLine($"ERROR: File not found:");
    Console.WriteLine(filePath);
    return;
}

Console.WriteLine($"Source: {filePath}");
Console.WriteLine();

string source = File.ReadAllText(
    filePath,
    Encoding.GetEncoding(1251));

var lexer = new DelphiLexer(source);

var tokens = lexer.Tokenize();

Console.WriteLine($"Lexer tokens: {tokens.Count}");
Console.WriteLine();

var parser = new DelphiParser(tokens);

DelphiUnit unit = parser.ParseUnit();

DelphiAstPrinter.Print(unit);