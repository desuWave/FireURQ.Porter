using System.Text;

using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

public static class ImplementationHeaderTrace
{
    public static void Analyze(string rootPath)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine(" Implementation Header Trace");
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
        int headersFound = 0;

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

                    string? kind = GetDeclarationKind(tokens, i);

                    if (kind == null)
                        continue;

                    HeaderTrace header =
                        ReadHeader(
                            tokens,
                            i,
                            kind);

                    headersFound++;

                    Console.WriteLine(
                        $"{Path.GetRelativePath(rootPath, file)}:" +
                        $"{token.Line}");

                    Console.WriteLine(
                        $"  {header.Text}");

                    Console.WriteLine(
                        $"  kind:       {header.Kind}");

                    Console.WriteLine(
                        $"  name:       {header.Name}");

                    Console.WriteLine(
                        $"  qualified:  " +
                        $"{(header.Name.Contains('.') ? "YES" : "NO")}");

                    Console.WriteLine(
                        $"  terminator: {header.Terminator}");

                    Console.WriteLine();
                }

                parsedFiles++;
            }
            catch (Exception ex)
            {
                failedFiles++;

                Console.WriteLine(
                    $"ERROR: {Path.GetRelativePath(rootPath, file)}");

                Console.WriteLine(
                    $"  {ex.Message}");

                Console.WriteLine();
            }
        }

        Console.WriteLine("--------------------------------");
        Console.WriteLine("Summary");
        Console.WriteLine("--------------------------------");

        Console.WriteLine(
            $"Files found:      {filesFound}");

        Console.WriteLine(
            $"Parsed:            {parsedFiles}");

        Console.WriteLine(
            $"Failed:            {failedFiles}");

        Console.WriteLine(
            $"Headers traced:    {headersFound}");

        Console.WriteLine();
    }

    private static HeaderTrace ReadHeader(
        IReadOnlyList<LexerToken> tokens,
        int position,
        string kind)
    {
        var parts =
            new List<string>();

        string name = "";

        string terminator = "";

        int depth = 0;

        for (int i = position;
             i < tokens.Count;
             i++)
        {
            LexerToken token = tokens[i];

            if (token.Kind == TokenKind.EndOfFile)
                break;

            if (IsSymbol(token, ";") && depth == 0)
            {
                terminator = ";";
                break;
            }

            if (IsSymbol(token, "=") && depth == 0)
            {
                terminator = "=";
                break;
            }

            if (IsSymbol(token, "(") ||
                IsSymbol(token, "["))
            {
                depth++;
            }
            else if (IsSymbol(token, ")") ||
                     IsSymbol(token, "]"))
            {
                if (depth > 0)
                    depth--;
            }

            parts.Add(token.Text);

            if (string.IsNullOrEmpty(name) &&
                i > position)
            {
                if (token.Kind == TokenKind.Identifier)
                {
                    name = ReadQualifiedName(
                        tokens,
                        i);
                }
            }
        }

        string text =
            string.Join(
                " ",
                parts);

        return new HeaderTrace(
            kind,
            name,
            text,
            terminator);
    }

    private static string ReadQualifiedName(
        IReadOnlyList<LexerToken> tokens,
        int position)
    {
        var parts =
            new List<string>();

        int i = position;

        while (i < tokens.Count)
        {
            LexerToken token = tokens[i];

            if (token.Kind != TokenKind.Identifier)
                break;

            parts.Add(token.Text);

            if (i + 2 >= tokens.Count)
                break;

            if (!IsSymbol(tokens[i + 1], "."))
                break;

            if (tokens[i + 2].Kind != TokenKind.Identifier)
                break;

            parts.Add(".");
            i += 2;
        }

        return string.Join("", parts);
    }

    private static string? GetDeclarationKind(
        IReadOnlyList<LexerToken> tokens,
        int position)
    {
        LexerToken token = tokens[position];

        if (IsKeyword(token, "function"))
        {
            if (position > 0 &&
                IsKeyword(
                    tokens[position - 1],
                    "class"))
            {
                return "class function";
            }

            return "function";
        }

        if (IsKeyword(token, "procedure"))
        {
            if (position > 0 &&
                IsKeyword(
                    tokens[position - 1],
                    "class"))
            {
                return "class procedure";
            }

            return "procedure";
        }

        if (IsKeyword(token, "constructor"))
            return "constructor";

        if (IsKeyword(token, "destructor"))
            return "destructor";

        return null;
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

    private static bool IsSymbol(
        LexerToken token,
        string symbol)
    {
        return token.Text == symbol;
    }

    private sealed record HeaderTrace(
        string Kind,
        string Name,
        string Text,
        string Terminator);
}