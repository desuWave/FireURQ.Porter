using System.Text;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class MethodImplementationCensus
{
    public static void Analyze(string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Method Implementation Census");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine(
                $"ERROR: Directory not found: {rootPath}");
            return;
        }

        string[] files =
            Directory.GetFiles(
                rootPath,
                "*.pas",
                SearchOption.AllDirectories);

        int filesFound = files.Length;
        int parsed = 0;
        int failed = 0;

        int totalFunctions = 0;
        int qualifiedFunctions = 0;
        int unqualifiedFunctions = 0;
        int qualifiedWithBody = 0;
        int qualifiedWithoutBody = 0;

        var implementations =
            new List<ImplementationInfo>();

        foreach (string file in files.OrderBy(x => x))
        {
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

                var unit =
                    parser.ParseUnit();

                parsed++;

                foreach (var declaration in unit.Declarations)
                {
                    if (declaration is not FireURQ.Porter.AST.DelphiFunction function)
                        continue;

                    totalFunctions++;

                    bool qualified =
                        function.Name.Contains('.');

                    if (!qualified)
                    {
                        unqualifiedFunctions++;
                        continue;
                    }

                    qualifiedFunctions++;

                    int bodyTokens =
                        function.BodyTokens.Count;

                    bool hasBody =
                        bodyTokens > 0;

                    if (hasBody)
                        qualifiedWithBody++;
                    else
                        qualifiedWithoutBody++;

                    implementations.Add(
                        new ImplementationInfo(
                            unit.Name,
                            function.Name,
                            bodyTokens,
                            Path.GetRelativePath(
                                rootPath,
                                file)));
                }
            }
            catch
            {
                failed++;
            }
        }

        Console.WriteLine("Summary");
        Console.WriteLine(
            $"Files found:              {filesFound}");

        Console.WriteLine(
            $"Parsed:                   {parsed}");

        Console.WriteLine(
            $"Failed:                   {failed}");

        Console.WriteLine();

        Console.WriteLine(
            $"Top-level DelphiFunctions: {totalFunctions}");

        Console.WriteLine(
            $"Qualified functions:      {qualifiedFunctions}");

        Console.WriteLine(
            $"Unqualified functions:    {unqualifiedFunctions}");

        Console.WriteLine(
            $"Qualified with body:      {qualifiedWithBody}");

        Console.WriteLine(
            $"Qualified without body:   {qualifiedWithoutBody}");

        Console.WriteLine();

        Console.WriteLine("--------------------------------");
        Console.WriteLine("Qualified implementations");
        Console.WriteLine("--------------------------------");

        foreach (ImplementationInfo implementation
                 in implementations)
        {
            Console.WriteLine(
                $"  {implementation.UnitName}: " +
                $"{implementation.Name} " +
                $"body={implementation.BodyTokens} " +
                $"[{implementation.File}]");
        }

        Console.WriteLine();
    }

    private sealed record ImplementationInfo(
        string UnitName,
        string Name,
        int BodyTokens,
        string File);
}
