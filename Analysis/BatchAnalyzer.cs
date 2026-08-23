using System.Text;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class BatchAnalyzer
{
    public static void Analyze(string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" FireURQ.Porter");
        Console.WriteLine(" Batch Analysis");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine("ERROR: Directory not found:");
            Console.WriteLine(rootPath);
            return;
        }

        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var files =
            Directory
                .GetFiles(
                    rootPath,
                    "*.pas",
                    SearchOption.AllDirectories)
                .OrderBy(path => path)
                .ToList();

        Console.WriteLine(
            $"Delphi files found: {files.Count}");

        Console.WriteLine();

        int success = 0;
        int failed = 0;

        var errors =
            new Dictionary<string, int>();

        foreach (var file in files)
        {
            string relativePath =
                Path.GetRelativePath(
                    rootPath,
                    file);

            Console.Write(
                $"{relativePath,-55}");

            try
            {
                string source =
                    File.ReadAllText(
                        file,
                        Encoding.GetEncoding(1251));

                var lexer =
                    new DelphiLexer(source);

                var tokens =
                    lexer.Tokenize();

                var parser =
                    new DelphiParser(tokens);

                parser.ParseUnit();

                Console.WriteLine(
                    $" OK   tokens={tokens.Count}");

                success++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $" FAIL {ex.Message}");

                failed++;

                string error =
                    ex.Message;

                if (errors.ContainsKey(error))
                    errors[error]++;
                else
                    errors[error] = 1;
            }
        }

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Summary");
        Console.WriteLine("================================");

        Console.WriteLine(
            $"Files:  {files.Count}");

        Console.WriteLine(
            $"Parsed: {success}");

        Console.WriteLine(
            $"Failed: {failed}");

        Console.WriteLine();

        if (errors.Count > 0)
        {
            Console.WriteLine(
                "Failure Categories:");

            foreach (var item in
                     errors
                         .OrderByDescending(
                             x => x.Value)
                         .ThenBy(x => x.Key))
            {
                Console.WriteLine(
                    $"  {item.Value,3}x  {item.Key}");
            }
        }

        Console.WriteLine();
    }
}