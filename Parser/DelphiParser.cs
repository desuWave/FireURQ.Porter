using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Parser;

    public sealed class DelphiParser
    {
    private readonly IReadOnlyList<LexerToken> _tokens;
    private int _position;

    public DelphiParser(IReadOnlyList<LexerToken> tokens)
    {
        _tokens = tokens;
    }

    // ========================================================
    // UNIT
    // ========================================================

    public DelphiUnit ParseUnit()
    {
        ExpectKeyword("unit");

        string name = ExpectIdentifier();

        ExpectSymbol(";");

        var uses = ParseUses();

        var declarations =
            new List<DelphiNode>();

        while (!IsEnd())
        {
            if (MatchKeyword("const"))
            {
                ParseConstSection(declarations);
                continue;
            }

            if (MatchKeyword("type"))
            {
                ParseTypeSection(declarations);
                continue;
            }

            if (MatchKeyword("function"))
            {
                declarations.Add(
                    ParseFunction(false));

                continue;
            }

            if (MatchKeyword("procedure"))
            {
                declarations.Add(
                    ParseFunction(true));

                continue;
            }

            if (MatchKeyword("implementation"))
                break;

            Advance();
        }

        return new DelphiUnit(
            name,
            uses,
            declarations);
    }

    // ========================================================
    // USES
    // ========================================================

    private List<string> ParseUses()
    {
        var result = new List<string>();

        if (!MatchKeyword("uses"))
            return result;

        while (!IsEnd())
        {
            result.Add(
                ExpectIdentifier());

            if (MatchSymbol(";"))
                break;

            MatchSymbol(",");
        }

        return result;
    }

    // ========================================================
    // CONST
    // ========================================================

    private void ParseConstSection(
        List<DelphiNode> declarations)
    {
        while (!IsEnd())
        {
            if (PeekKeyword("type") ||
                PeekKeyword("var") ||
                PeekKeyword("function") ||
                PeekKeyword("procedure") ||
                PeekKeyword("implementation"))
            {
                return;
            }

            if (!IsIdentifier())
            {
                Advance();
                continue;
            }

            string name =
                ExpectIdentifier();

            string type = "";

            if (MatchSymbol(":"))
            {
            type = ReadTypeExpression();
            }

            if (!MatchSymbol("="))
            continue;

            string value =
            ReadUntil(";");

            declarations.Add(
            new DelphiConst(
            name,
            type,
            value));

            MatchSymbol(";");
        }
    }

    // ========================================================
    // TYPE
    // ========================================================

    private void ParseTypeSection(
    List<DelphiNode> declarations)
    {
    while (!IsEnd())
    {
        if (PeekKeyword("const") ||
            PeekKeyword("var") ||
            PeekKeyword("function") ||
            PeekKeyword("procedure") ||
            PeekKeyword("implementation"))
        {
            return;
        }

        if (!IsIdentifier())
        {
            Advance();
            continue;
        }

        string name =
            ExpectIdentifier();

        if (!MatchSymbol("="))
            continue;

        // ------------------------------------------------
        // ENUM
        // ------------------------------------------------

        if (MatchSymbol("("))
        {
            var values =
                new List<string>();

            while (!IsEnd() &&
                   !PeekSymbol(")"))
            {
                if (IsIdentifier())
                {
                    values.Add(
                        ExpectIdentifier());
                }
                else
                {
                    Advance();
                }

                MatchSymbol(",");
            }

            ExpectSymbol(")");
            MatchSymbol(";");

            declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Enum,
                    "enum"));

            declarations.Add(
                new DelphiEnum(
                    name,
                    values));

            continue;
        }

        // ------------------------------------------------
        // SET
        // ------------------------------------------------

        if (MatchKeyword("set"))
        {
            MatchKeyword("of");

            string elementType =
                ExpectTypeName();

            MatchSymbol(";");

            declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Set,
                    $"set of {elementType}"));

            continue;
        }

        // ------------------------------------------------
        // CLASS / OBJECT
        // ------------------------------------------------

        if (MatchKeyword("class") ||
            MatchKeyword("object"))
        {
            var definition =
                new List<string>();

            definition.Add("class");

            if (MatchSymbol("("))
            {
                definition.Add("(");

                while (!IsEnd() &&
                       !PeekSymbol(")"))
                {
                    definition.Add(
                        Current().Text);

                    Advance();
                }

                ExpectSymbol(")");

                definition.Add(")");
            }

            // Пока не разбираем тело класса.
            // Просто пропускаем его до ';'.
            while (!IsEnd() &&
                   !PeekSymbol(";"))
            {
                Advance();
            }

            MatchSymbol(";");

            declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Class,
                    string.Join(" ", definition)));

            continue;
        }

        // ------------------------------------------------
        // RECORD
        // ------------------------------------------------

        if (MatchKeyword("record"))
        {
            while (!IsEnd() &&
                   !PeekSymbol(";"))
            {
                Advance();
            }

            MatchSymbol(";");

            declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Record,
                    "record"));

            continue;
        }

        // ------------------------------------------------
        // PROCEDURE TYPE
        // ------------------------------------------------

        if (MatchKeyword("procedure") ||
            MatchKeyword("function"))
        {
            var definition =
                new List<string>();

            definition.Add(
                "procedure");

            int depth = 0;

            while (!IsEnd())
            {
                if (PeekSymbol(";") &&
                    depth == 0)
                {
                    break;
                }

                if (PeekSymbol("("))
                    depth++;

                if (PeekSymbol(")"))
                    depth--;

                definition.Add(
                    Current().Text);

                Advance();
            }

            MatchSymbol(";");

            declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Procedure,
                    string.Join(
                        " ",
                        definition)));

            continue;
        }

        // ------------------------------------------------
        // SIMPLE / UNKNOWN TYPE
        // ------------------------------------------------

        var parts =
            new List<string>();

        while (!IsEnd() &&
               !PeekSymbol(";"))
        {
            parts.Add(
                Current().Text);

            Advance();
        }

        MatchSymbol(";");

        declarations.Add(
            new DelphiType(
                name,
                DelphiTypeKind.Unknown,
                string.Join(
                    " ",
                    parts)));
    }
    }

    // ========================================================
    // FUNCTION / PROCEDURE
    // ========================================================

    private DelphiFunction ParseFunction(
        bool isProcedure)
    {
        string name =
            ExpectIdentifier();

        var parameters =
            new List<DelphiParameter>();

        if (MatchSymbol("("))
        {
            while (!IsEnd() &&
                   !PeekSymbol(")"))
            {
                bool isConst =
                    MatchKeyword("const");

                string parameterName =
                    ExpectIdentifier();

                ExpectSymbol(":");

                string type =
                    ExpectTypeName();

                parameters.Add(
                    new DelphiParameter(
                        parameterName,
                        type,
                        isConst));

                MatchSymbol(";");
                MatchSymbol(",");
            }

            ExpectSymbol(")");
        }

        string returnType = "";

        if (!isProcedure)
        {
            ExpectSymbol(":");

            returnType =
                ExpectTypeName();
        }

        MatchSymbol(";");

        return new DelphiFunction(
            name,
            parameters,
            returnType,
            isProcedure);
    }

    // ========================================================
    // HELPERS
    // ========================================================

    private string ReadUntil(string symbol)
    {
        var parts =
            new List<string>();

        while (!IsEnd() &&
               !PeekSymbol(symbol))
        {
            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private bool IsEnd()
    {
        return _position >= _tokens.Count ||
               Current().Kind == TokenKind.EndOfFile;
    }

    private LexerToken Current()
    {
        return _tokens[_position];
    }

    private void Advance()
    {
        if (!IsEnd())
            _position++;
    }

    private bool IsIdentifier()
    {
        return !IsEnd() &&
               Current().Kind ==
                   TokenKind.Identifier;
    }

    private bool IsKeyword()
    {
        return !IsEnd() &&
               Current().Kind ==
                   TokenKind.Keyword;
    }

    private bool PeekKeyword(string value)
    {
        return !IsEnd() &&
               IsKeyword() &&
               string.Equals(
                   Current().Text,
                   value,
                   StringComparison.OrdinalIgnoreCase);
    }

    private bool PeekSymbol(string value)
    {
        return !IsEnd() &&
               Current().Kind ==
                   TokenKind.Symbol &&
               Current().Text == value;
    }

    private bool MatchKeyword(string value)
    {
        if (!PeekKeyword(value))
            return false;

        Advance();

        return true;
    }

    private bool MatchSymbol(string value)
    {
        if (!PeekSymbol(value))
            return false;

        Advance();

        return true;
    }

    private void ExpectKeyword(string value)
    {
        if (!MatchKeyword(value))
        {
            throw new Exception(
                $"Expected keyword '{value}'");
        }
    }


    private void ExpectSymbol(string value)
    {
        if (!MatchSymbol(value))
        {
            throw new Exception(
                $"Expected symbol '{value}'");
        }
    }

    private string ExpectTypeName()
    {
    if (IsEnd())
        throw new Exception(
            "Expected type name, got EOF");

    if (Current().Kind == TokenKind.Identifier ||
        Current().Kind == TokenKind.Keyword)
    {
        string value = Current().Text;

        Advance();

        return value;
    }

    throw new Exception(
        $"Expected type name, got '{Current().Text}'");
    }

    private string ReadTypeExpression()
    {
    var parts = new List<string>();

    while (!IsEnd() &&
           !PeekSymbol("="))
    {
        parts.Add(Current().Text);
        Advance();
    }

    return string.Join(" ", parts);
    }

    private string ExpectIdentifier()
    {
    if (!IsIdentifier())
    {
        throw new Exception(
            $"Expected identifier, got '{Current().Text}'");
    }

    string value = Current().Text;

    Advance();

    return value;

    
    }
    }