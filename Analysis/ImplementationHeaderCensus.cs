using System.Text;

using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

public static class ImplementationHeaderCensus
{
    public static void Analyze(string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Implementation Header Census");
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
        int parsedFiles = 0;
        int failedFiles = 0;

        int functions = 0;
        int procedures = 0;
        int constructors = 0;
        int destructors = 0;
        int classFunctions = 0;
        int classProcedures = 0;

        var headers =
            new List<HeaderInfo>();

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

                IReadOnlyList<LexerToken> tokens =
                    lexer.Tokenize();

                bool inImplementation = false;
                int beginDepth = 0;

                for (int i = 0; i < tokens.Count; i++)
                {
                    LexerToken token = tokens[i];

                    if (!inImplementation)
                    {
                        if (IsKeyword(token, "implementation"))
                        {
                            inImplementation = true;
                        }

                        continue;
                    }

                    if (token.Kind == TokenKind.EndOfFile)
                        break;

                    /*
                     * We only inspect declaration level.
                     *
                     * Once a method body starts with "begin",
                     * beginDepth becomes greater than zero.
                     *
                     * This prevents procedure/function keywords
                     * occurring inside method bodies from being
                     * counted as implementation headers.
                     */
                    if (beginDepth > 0)
                    {
                        if (IsKeyword(token, "begin"))
                        {
                            beginDepth++;
                        }
                        else if (IsKeyword(token, "end"))
                        {
                            beginDepth--;

                            if (beginDepth < 0)
                                beginDepth = 0;
                        }

                        continue;
                    }

                    if (IsKeyword(token, "begin"))
                    {
                        beginDepth = 1;
                        continue;
                    }

                    if (IsKeyword(token, "function"))
                    {
                        if (IsPreviousClassModifier(tokens, i))
                        {
                            classFunctions++;

                            headers.Add(
                                new HeaderInfo(
                                    Path.GetRelativePath(
                                        rootPath,
                                        file),
                                    "class function",
                                    token.Line));
                        }
                        else
                        {
                            functions++;

                            headers.Add(
                                new HeaderInfo(
                                    Path.GetRelativePath(
                                        rootPath,
                                        file),
                                    "function",
                                    token.Line));
                        }

                        continue;
                    }

                    if (IsKeyword(token, "procedure"))
                    {
                        if (IsPreviousClassModifier(tokens, i))
                        {
                            classProcedures++;

                            headers.Add(
                                new HeaderInfo(
                                    Path.GetRelativePath(
                                        rootPath,
                                        file),
                                    "class procedure",
                                    token.Line));
                        }
                        else
                        {
                            procedures++;

                            headers.Add(
                                new HeaderInfo(
                                    Path.GetRelativePath(
                                        rootPath,
                                        file),
                                    "procedure",
                                    token.Line));
                        }

                        continue;
                    }

                    if (IsKeyword(token, "constructor"))
                    {
                        constructors++;

                        headers.Add(
                            new HeaderInfo(
                                Path.GetRelativePath(
                                    rootPath,
                                    file),
                                "constructor",
                                token.Line));

                        continue;
                    }

                    if (IsKeyword(token, "destructor"))
                    {
                        destructors++;

                        headers.Add(
                            new HeaderInfo(
                                Path.GetRelativePath(
                                    rootPath,
                                    file),
                                "destructor",
                                token.Line));

                        continue;
                    }
                }

                parsedFiles++;
            }
            catch
            {
                failedFiles++;
            }
        }

        Console.WriteLine("Summary");
        Console.WriteLine(
            $"Files found:              {filesFound}");

        Console.WriteLine(
            $"Parsed:                   {parsedFiles}");

        Console.WriteLine(
            $"Failed:                   {failedFiles}");

        Console.WriteLine();

        Console.WriteLine(
            $"function:                 {functions}");

        Console.WriteLine(
            $"procedure:                {procedures}");

        Console.WriteLine(
            $"constructor:              {constructors}");

        Console.WriteLine(
            $"destructor:               {destructors}");

        Console.WriteLine(
            $"class function:           {classFunctions}");

        Console.WriteLine(
            $"class procedure:          {classProcedures}");

        Console.WriteLine();

        Console.WriteLine(
            $"Total headers:            {headers.Count}");

        Console.WriteLine();

        Console.WriteLine("--------------------------------");
        Console.WriteLine("Implementation headers");
        Console.WriteLine("--------------------------------");

        foreach (HeaderInfo header in headers)
        {
            Console.WriteLine(
                $"  {header.File}:{header.Line} " +
                $"{header.Kind}");
        }

        Console.WriteLine();
    }

    private static bool IsPreviousClassModifier(
        IReadOnlyList<LexerToken> tokens,
        int position)
    {
        if (position <= 0)
            return false;

        LexerToken previous =
            tokens[position - 1];

        return IsKeyword(
            previous,
            "class");
    }

    private static bool IsKeyword(
        LexerToken token,
        string keyword)
    {
        return token.Kind == TokenKind.Keyword &&
               token.Text.Equals(
                   keyword,
                   StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HeaderInfo(
        string File,
        string Kind,
        int Line);
}
