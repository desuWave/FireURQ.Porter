using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

/// <summary>
/// Removes inactive Delphi conditional-compilation branches before parsing.
/// Conditional symbols are case-insensitive.
/// </summary>
public sealed class DelphiConditionalPreprocessor
{
    private readonly HashSet<string> _symbols;

    public DelphiConditionalPreprocessor(
        IEnumerable<string>? definedSymbols = null)
    {
        _symbols = new HashSet<string>(
            definedSymbols ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public List<LexerToken> Process(
        IReadOnlyList<LexerToken> tokens)
    {
        var output = new List<LexerToken>(tokens.Count);
        var stack = new Stack<ConditionalFrame>();
        bool active = true;

        foreach (LexerToken token in tokens)
        {
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
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            string command = parts[0].ToUpperInvariant();

            switch (command)
            {
                case "IFDEF":
                case "IFNDEF":
                {
                    if (parts.Length < 2)
                    {
                        throw Error(token,
                            $"{command} requires a conditional symbol.");
                    }

                    bool defined = _symbols.Contains(parts[1]);
                    bool condition = command == "IFDEF"
                        ? defined
                        : !defined;

                    stack.Push(new ConditionalFrame(
                        ParentActive: active,
                        Condition: condition,
                        ElseSeen: false,
                        Line: token.Line));

                    active = active && condition;
                    break;
                }

                case "ELSE":
                {
                    if (stack.Count == 0)
                        throw Error(token, "ELSE without matching IFDEF/IFNDEF.");

                    ConditionalFrame frame = stack.Pop();

                    if (frame.ElseSeen)
                        throw Error(token, "Duplicate ELSE in the same conditional block.");

                    frame = frame with { ElseSeen = true };
                    stack.Push(frame);

                    active = frame.ParentActive && !frame.Condition;
                    break;
                }

                case "ENDIF":
                {
                    if (stack.Count == 0)
                        throw Error(token, "ENDIF without matching IFDEF/IFNDEF.");

                    ConditionalFrame frame = stack.Pop();
                    active = frame.ParentActive;
                    break;
                }

                case "DEFINE":
                {
                    if (active && parts.Length >= 2)
                        _symbols.Add(parts[1]);
                    break;
                }

                case "UNDEF":
                {
                    if (active && parts.Length >= 2)
                        _symbols.Remove(parts[1]);
                    break;
                }

                default:
                    // Other compiler directives are not conditional delimiters.
                    // The parser already ignores directive tokens.
                    break;
            }
        }

        if (stack.Count > 0)
        {
            ConditionalFrame frame = stack.Peek();
            throw new InvalidOperationException(
                $"Unclosed conditional directive opened at line {frame.Line}.");
        }

        if (output.Count == 0 ||
            output[^1].Kind != TokenKind.EndOfFile)
        {
            LexerToken eof = tokens.LastOrDefault(
                token => token.Kind == TokenKind.EndOfFile)
                ?? new LexerToken(TokenKind.EndOfFile, string.Empty, 1, 1);

            output.Add(eof);
        }

        return output;
    }

    private static InvalidOperationException Error(
        LexerToken token,
        string message) =>
        new($"Conditional compilation error at line {token.Line}, column {token.Column}: {message}");

    private sealed record ConditionalFrame(
        bool ParentActive,
        bool Condition,
        bool ElseSeen,
        int Line);
}
