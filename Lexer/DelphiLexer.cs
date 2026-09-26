using System.Text;

namespace FireURQ.Porter.Lexer;

public sealed class DelphiLexer
{
    private readonly string _source;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    // =========================================================
    // CHARACTER CODE LITERALS
    // =========================================================

    private LexerToken ReadCharacterCode()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        while (!IsAtEnd() && Current() == '#')
        {
            builder.Append(Current());
            Advance();

            while (!IsAtEnd() &&
                   char.IsDigit(Current()))
            {
                builder.Append(Current());
                Advance();
            }
        }

        return new LexerToken(
            TokenKind.Character,
            builder.ToString(),
            line,
            column);
    }

    private static readonly HashSet<string> Keywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "unit",
            "interface",
            "implementation",
            "uses",
            "const",
            "type",
            "var",
            "function",
            "procedure",
            "class",
            "record",
            "packed",
            "object",
            "read",
            "write",
            "default",
            "set",
            "of",
            "array",
            "string",
            "char",
            "integer",
            "cardinal",
            "boolean",
            "extended",
            "double",
            "single",
            "begin",
            "end",
            "if",
            "then",
            "else",
            "for",
            "while",
            "repeat",
            "until",
            "do",
            "to",
            "downto",
            "case",
            "try",
            "except",
            "finally",
            "nil",
            "true",
            "false",
            "and",
            "or",
            "not",
            "in",
            "out",
            "private",
            "protected",
            "public",
            "published",
            "virtual",
            "override",
            "abstract",
            "constructor",
            "destructor",
            "property"
        };

    public DelphiLexer(string source)
    {
        _source = source;
    }

    public List<LexerToken> Tokenize()
    {
        var tokens =
            new List<LexerToken>();

        while (!IsAtEnd())
        {
            SkipWhitespace();

            if (IsAtEnd())
                break;

            int line = _line;
            int column = _column;
            char c = Current();

            // -------------------------------------------------
            // Comments
            // -------------------------------------------------

            if (c == '/' && Peek(1) == '/')
            {
                ReadLineComment();
                continue;
            }

            if (c == '{')
            {
                ReadBraceComment();
                continue;
            }

            if (c == '(' && Peek(1) == '*')
            {
                ReadBlockComment();
                continue;
            }

            // -------------------------------------------------
            // String / character literal
            // -------------------------------------------------

            if (c == '\'')
            {
                tokens.Add(ReadString());
                continue;
            }

            // -------------------------------------------------
            // Identifier / keyword
            // -------------------------------------------------

            if (IsIdentifierStart(c))
            {
                string text =
                    ReadIdentifier();

                TokenKind kind =
                    Keywords.Contains(text)
                        ? TokenKind.Keyword
                        : TokenKind.Identifier;

                tokens.Add(
                    new LexerToken(
                        kind,
                        text,
                        line,
                        column));

                continue;
            }

            // -------------------------------------------------
            // Character code literal
            //
            // Delphi:
            // #9
            // #13
            // #13#10
            // -------------------------------------------------

            if (c == '#' && char.IsDigit(Peek(1)))
            {
                tokens.Add(ReadCharacterCode());
                continue;
            }

            // -------------------------------------------------
            // Hex number
            //
            // Delphi:
            // $3F
            // $67452301
            // $EFCDAB89
            // -------------------------------------------------

            if (c == '$' && IsHexDigit(Peek(1)))
            {
                tokens.Add(ReadHexNumber());
                continue;
            }

            // -------------------------------------------------
            // Number
            // -------------------------------------------------

            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // -------------------------------------------------
            // Operators
            // -------------------------------------------------

            string? op =
                ReadOperator();

            if (op != null)
            {
                tokens.Add(
                    new LexerToken(
                        TokenKind.Operator,
                        op,
                        line,
                        column));

                continue;
            }

            // -------------------------------------------------
            // Symbol
            // -------------------------------------------------

            Advance();

            tokens.Add(
                new LexerToken(
                    TokenKind.Symbol,
                    c.ToString(),
                    line,
                    column));
        }

        tokens.Add(
            new LexerToken(
                TokenKind.EndOfFile,
                string.Empty,
                _line,
                _column));

        return tokens;
    }

    // =========================================================
    // IDENTIFIERS
    // =========================================================

    private string ReadIdentifier()
    {
        var builder =
            new StringBuilder();

        while (!IsAtEnd() &&
               IsIdentifierPart(Current()))
        {
            builder.Append(Current());
            Advance();
        }

        return builder.ToString();
    }

    private static bool IsIdentifierStart(char c)
    {
        return char.IsLetter(c) ||
               c == '_';
    }

    private static bool IsIdentifierPart(char c)
    {
        return char.IsLetterOrDigit(c) ||
               c == '_';
    }

    // =========================================================
    // NUMBERS
    // =========================================================

    private LexerToken ReadNumber()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        while (!IsAtEnd() &&
               char.IsDigit(Current()))
        {
            builder.Append(Current());
            Advance();
        }

        if (!IsAtEnd() &&
            Current() == '.' &&
            char.IsDigit(Peek(1)))
        {
            builder.Append(Current());
            Advance();

            while (!IsAtEnd() &&
                   char.IsDigit(Current()))
            {
                builder.Append(Current());
                Advance();
            }
        }

        return new LexerToken(
            TokenKind.Number,
            builder.ToString(),
            line,
            column);
    }

    private LexerToken ReadHexNumber()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        // Opening '$'.
        builder.Append(Current());
        Advance();

        while (!IsAtEnd() &&
               IsHexDigit(Current()))
        {
            builder.Append(Current());
            Advance();
        }

        return new LexerToken(
            TokenKind.Number,
            builder.ToString(),
            line,
            column);
    }

    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) ||
               (c >= 'a' && c <= 'f') ||
               (c >= 'A' && c <= 'F');
    }

    // =========================================================
    // STRINGS
    // =========================================================

    private LexerToken ReadString()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        // Opening apostrophe.
        builder.Append(Current());
        Advance();

        while (!IsAtEnd())
        {
            char c =
                Current();

            if (c == '\'')
            {
                // Delphi escapes apostrophe as ''.
                if (Peek(1) == '\'')
                {
                    builder.Append(Current());
                    Advance();

                    builder.Append(Current());
                    Advance();

                    continue;
                }

                // Closing apostrophe.
                builder.Append(Current());
                Advance();

                break;
            }

            builder.Append(c);
            Advance();
        }

        return new LexerToken(
            TokenKind.String,
            builder.ToString(),
            line,
            column);
    }

    // =========================================================
    // COMMENTS
    // =========================================================

    private LexerToken ReadLineComment()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        while (!IsAtEnd() &&
               Current() != '\n')
        {
            builder.Append(Current());
            Advance();
        }

        return new LexerToken(
            TokenKind.Comment,
            builder.ToString(),
            line,
            column);
    }

    private LexerToken ReadBraceComment()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        Advance(); // {

        while (!IsAtEnd() &&
               Current() != '}')
        {
            builder.Append(Current());
            Advance();
        }

        if (!IsAtEnd())
            Advance();

        return new LexerToken(
            TokenKind.Comment,
            builder.ToString(),
            line,
            column);
    }

    private LexerToken ReadBlockComment()
    {
        int line = _line;
        int column = _column;

        var builder =
            new StringBuilder();

        Advance(); // (
        Advance(); // *

        while (!IsAtEnd())
        {
            if (Current() == '*' &&
                Peek(1) == ')')
            {
                Advance();
                Advance();
                break;
            }

            builder.Append(Current());
            Advance();
        }

        return new LexerToken(
            TokenKind.Comment,
            builder.ToString(),
            line,
            column);
    }

    // =========================================================
    // OPERATORS
    // =========================================================

    private string? ReadOperator()
    {
        char c =
            Current();

        char next =
            Peek(1);

        string? result = null;

        if (c == ':' && next == '=')
            result = ":=";
        else if (c == '<' && next == '=')
            result = "<=";
        else if (c == '>' && next == '=')
            result = ">=";
        else if (c == '<' && next == '>')
            result = "<>";
        else if (c == '.' && next == '.')
            result = "..";

        if (result != null)
        {
            Advance();
            Advance();
        }

        return result;
    }

    // =========================================================
    // CHARACTER / POSITION
    // =========================================================

    private char Current()
    {
        return _source[_position];
    }

    private char Peek(int offset)
    {
        int index =
            _position + offset;

        if (index >= _source.Length)
            return '\0';

        return _source[index];
    }

    private void Advance()
    {
        if (IsAtEnd())
            return;

        if (_source[_position] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _position++;
    }

    private void SkipWhitespace()
    {
        while (!IsAtEnd() &&
               char.IsWhiteSpace(Current()))
        {
            Advance();
        }
    }

    private bool IsAtEnd()
    {
        return _position >= _source.Length;
    }
}


