using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

public sealed class DelphiConditionalPreprocessor
{
    private readonly string[] _initialSymbols;

    public DelphiConditionalPreprocessor(
        IEnumerable<string>? definedSymbols = null)
    {
        _initialSymbols = definedSymbols?.ToArray() ?? Array.Empty<string>();
    }

    public bool TryProcess(
        IReadOnlyList<LexerToken> tokens,
        out List<LexerToken> output,
        out string? error,
        string? sourcePath = null)
    {
        output = new List<LexerToken>(tokens.Count);
        error = null;

        var symbols = new HashSet<string>(
            _initialSymbols, StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<Frame>();
        bool active = true;

        var work = tokens.Select(token =>
            new WorkItem(
                token,
                sourcePath,
                sourcePath is null
                    ? Array.Empty<string>()
                    : new[] { System.IO.Path.GetFullPath(sourcePath) }))
            .ToList();

        for (int index = 0; index < work.Count; index++)
        {
            WorkItem item = work[index];
            LexerToken token = item.Token;
            if (token.Kind != TokenKind.Directive)
            {
                if (active || token.Kind == TokenKind.EndOfFile)
                    output.Add(token);
                continue;
            }

            string directive = token.Text.Trim();
            if (directive.StartsWith('$'))
                directive = directive[1..].Trim();

            string[] parts = directive.Split(
                (char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            string command = parts[0].ToUpperInvariant();
            string? problem = null;

            switch (command)
            {
                case "IFDEF":
                case "IFNDEF":
                    if (parts.Length != 2)
                    {
                        problem = $"{command} requires one symbol.";
                        break;
                    }

                    bool condition = symbols.Contains(parts[1]);
                    if (command == "IFNDEF")
                        condition = !condition;

                    stack.Push(new Frame(active, condition, false, token.Line));
                    active = active && condition;
                    break;

                case "ELSE":
                    if (stack.Count == 0)
                    {
                        problem = "ELSE without a matching conditional.";
                        break;
                    }

                    Frame alternative = stack.Pop();
                    if (alternative.ElseSeen)
                    {
                        problem = "Duplicate ELSE.";
                        break;
                    }

                    stack.Push(alternative with { ElseSeen = true });
                    active = alternative.ParentActive && !alternative.Condition;
                    break;

                case "ENDIF":
                    if (stack.Count == 0)
                    {
                        problem = "ENDIF without a matching conditional.";
                        break;
                    }

                    active = stack.Pop().ParentActive;
                    break;

                case "DEFINE":
                case "UNDEF":
                    if (parts.Length != 2)
                    {
                        problem = $"{command} requires one symbol.";
                        break;
                    }

                    if (active)
                    {
                        if (command == "DEFINE")
                            symbols.Add(parts[1]);
                        else
                            symbols.Remove(parts[1]);
                    }
                    break;

                case "I":
                case "INCLUDE":
                    if (!active)
                        break;

                    if (item.SourcePath is null)
                    {
                        problem = "Include requires the source file path.";
                        break;
                    }

                    string includeName = directive[parts[0].Length..].Trim();

                    if (includeName.Length >= 2 &&
                        ((includeName[0] == '\'' && includeName[^1] == '\'') ||
                         (includeName[0] == '"' && includeName[^1] == '"')))
                        includeName = includeName[1..^1];

                    if (string.IsNullOrWhiteSpace(includeName))
                    {
                        problem = "Include file name is empty.";
                        break;
                    }

                    try
                    {
                        string includePath = System.IO.Path.GetFullPath(
                            System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(
                                    System.IO.Path.GetFullPath(item.SourcePath))!,
                                includeName));

                        if (item.Chain.Contains(
                            includePath, StringComparer.OrdinalIgnoreCase))
                        {
                            problem = $"Include cycle: {includePath}";
                            break;
                        }

                        if (item.Chain.Count >= 64)
                        {
                            problem = "Include nesting exceeds 64 files.";
                            break;
                        }

                        string includeSource = System.IO.File.ReadAllText(
                            includePath, System.Text.Encoding.GetEncoding(1251));

                        var chain = item.Chain.Append(includePath).ToArray();
                        var included = new DelphiLexer(includeSource)
                            .Tokenize()
                            .Where(value => value.Kind != TokenKind.EndOfFile)
                            .Select(value => new WorkItem(
                                value, includePath, chain))
                            .ToList();

                        work.InsertRange(index + 1, included);
                    }
                    catch (Exception exception)
                    {
                        problem = $"Cannot read include '{includeName}': " +
                            exception.Message;
                    }
                    break;

                case "IF":
                case "IFOPT":
                case "ELSEIF":
                case "ELIF":
                case "IFEND":

                    problem = $"Unsupported preprocessing directive: {directive}";
                    break;

                default:
                    // Ordinary compiler options do not affect branch selection.
                    break;
            }

            if (problem is not null)
            {
                error = $"{item.SourcePath ?? "<tokens>"}: line {token.Line}, column {token.Column}: {problem}";
                output.Clear();
                return false;
            }
        }

        if (stack.Count > 0)
        {
            error = $"Unclosed conditional starting at line {stack.Peek().Line}.";
            output.Clear();
            return false;
        }

        return true;
    }

    private sealed record WorkItem(
        LexerToken Token,
        string? SourcePath,
        IReadOnlyList<string> Chain);

    private sealed record Frame(
        bool ParentActive,
        bool Condition,
        bool ElseSeen,
        int Line);
}
