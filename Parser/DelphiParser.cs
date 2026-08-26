using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Parser;

public sealed class DelphiParser
{
    private readonly IReadOnlyList<LexerToken> _tokens;
    private int _position;

    private DelphiProperty ParseProperty()
    {
        string name =
            ExpectIdentifier();

        var parameters =
            new List<DelphiParameter>();

        if (MatchSymbol("["))
        {
            while (!IsEnd() &&
                   !PeekSymbol("]"))
            {
                var parameterKind =
                    DelphiParameterKind.Value;

                if (MatchKeyword("const"))
                    parameterKind = DelphiParameterKind.Const;
                else if (MatchKeyword("var"))
                    parameterKind = DelphiParameterKind.Var;
                else if (MatchKeyword("out"))
                    parameterKind = DelphiParameterKind.Out;

                var parameterNames =
                    new List<string>();

                parameterNames.Add(
                    ExpectIdentifier());

                while (MatchSymbol(","))
                {
                    parameterNames.Add(
                        ExpectIdentifier());
                }

                string parameterType = "";

                if (MatchSymbol(":"))
                {
                    parameterType =
                        ReadParameterType();
                }

                foreach (var parameterName in parameterNames)
                {
                    parameters.Add(
                        new DelphiParameter(
                            parameterName,
                            parameterType,
                            parameterKind));
                }

                MatchSymbol(";");
            }

            ExpectSymbol("]");
        }

        string type = "";

        if (MatchSymbol(":"))
        {
            type =
                ReadParameterType();
        }

        string? readAccessor = null;
        string? writeAccessor = null;

        bool isDefault = false;

        while (!IsEnd() &&
               !PeekSymbol(";"))
        {
            if (MatchKeyword("read"))
            {
                readAccessor =
                    ReadQualifiedIdentifier();

                continue;
            }

            if (MatchKeyword("write"))
            {
                writeAccessor =
                    ReadQualifiedIdentifier();

                continue;
            }

            if (MatchKeyword("default"))
            {
                isDefault = true;

                continue;
            }

            Advance();
        }

        MatchSymbol(";");

        return new DelphiProperty(
            name,
            type,
            parameters,
            readAccessor,
            writeAccessor,
            isDefault);
    }

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
            // POINTER
            // ------------------------------------------------

            if (MatchSymbol("^"))
            {
                string targetType = ExpectTypeName();

                MatchSymbol(";");

                declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Pointer,
                    $"^{targetType}"));

                continue;
            }

            // ------------------------------------------------
            // ARRAY
            // ------------------------------------------------

            if (MatchKeyword("array"))
            {
                var definition = new List<string>
        {
        "array"
        };

                if (MatchSymbol("["))
                {
                    definition.Add("[");

                    while (!IsEnd() && !PeekSymbol("]"))
                    {
                        definition.Add(Current().Text);
                        Advance();
                    }

                    ExpectSymbol("]");

                    definition.Add("]");
                }

                if (MatchKeyword("of"))
                {
                    definition.Add("of");
                    definition.Add(ExpectTypeName());
                }

                MatchSymbol(";");

                declarations.Add(
                new DelphiType(
                    name,
                    DelphiTypeKind.Array,
                    string.Join(" ", definition)));

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
                string? baseType = null;

                // ------------------------------------------------
                // Optional ancestor
                // ------------------------------------------------

                // ------------------------------------------------
                // Optional ancestor
                // ------------------------------------------------

                if (MatchSymbol("("))
                {
                    baseType = ReadQualifiedIdentifier();

                    // Пока поддерживаем первого предка.
                    // Остальные типы после ',' пропускаем.
                    while (MatchSymbol(","))
                    {
                        Advance();
                    }

                    ExpectSymbol(")");
                }

                // ------------------------------------------------
                // Empty / forward-style class declaration
                // ------------------------------------------------

                if (MatchSymbol(";"))
                {
                    declarations.Add(
                        new DelphiClass(
                            name,
                            baseType,
                            new List<DelphiClassMember>()));

                    continue;
                }

                var members =
                    new List<DelphiClassMember>();

                var visibility =
                    DelphiVisibility.Public;

                // ------------------------------------------------
                // CLASS BODY
                // ------------------------------------------------

                while (!IsEnd())
                {
                    // --------------------------------------------
                    // End of class
                    // --------------------------------------------

                    if (PeekKeyword("end"))
                    {
                        Advance();
                        MatchSymbol(";");
                        break;
                    }

                    // --------------------------------------------
                    // Visibility
                    // --------------------------------------------

                    if (MatchKeyword("private"))
                    {
                        visibility =
                            DelphiVisibility.Private;

                        continue;
                    }

                    if (MatchKeyword("protected"))
                    {
                        visibility =
                            DelphiVisibility.Protected;

                        continue;
                    }

                    if (MatchKeyword("public"))
                    {
                        visibility =
                            DelphiVisibility.Public;

                        continue;
                    }

                    if (MatchKeyword("published"))
                    {
                        visibility =
                            DelphiVisibility.Published;

                        continue;
                    }

                    if (MatchKeyword("automated"))
                    {
                        visibility =
                            DelphiVisibility.Automated;

                        continue;
                    }

                    // --------------------------------------------
                    // Property
                    //
                    // property Name: Type read Getter write Setter;
                    // --------------------------------------------

                    if (MatchKeyword("property"))
                    {
                        var property =
                            ParseProperty();

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Property,
                                property));

                        continue;
                    }

                    // ------------------------------------------------
                    // Interface method resolution
                    //
                    // function Interface.Method = ImplementationMethod;
                    // ------------------------------------------------

                    if (PeekKeyword("function"))
                    {
                        int savedPosition = _position;

                        Advance(); // function

                        string firstName =
                            ReadQualifiedIdentifier();

                        if (MatchSymbol("="))
                        {
                            string implementationMethod =
                                ReadQualifiedIdentifier();

                            MatchSymbol(";");

                            members.Add(
                                new DelphiClassMember(
                                    visibility,
                                    DelphiClassMemberKind.MethodResolution,
                                    new DelphiMethodResolution(
                                        firstName,
                                        implementationMethod)));

                            continue;
                        }

                        _position = savedPosition;
                    }

                    // --------------------------------------------
                    // Method declaration
                    // --------------------------------------------

                    if (MatchKeyword("procedure"))
                    {
                        var function =
                            ParseFunction(true);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }

                    if (MatchKeyword("function"))
                    {
                        var function =
                            ParseFunction(false);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }

                    if (MatchKeyword("constructor"))
                    {
                        var function =
                            ParseFunction(true);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }



                    if (MatchKeyword("destructor"))
                    {
                        var function =
                            ParseFunction(true);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }

                    // --------------------------------------------
                    // Field
                    //
                    // name: Type;
                    // --------------------------------------------

                    if (IsIdentifier())
                    {
                        string fieldName =
                            ExpectIdentifier();

                        if (MatchSymbol(":"))
                        {
                            string fieldType =
                                ReadParameterType();

                            MatchSymbol(";");

                            members.Add(
                                new DelphiClassMember(
                                    visibility,
                                    DelphiClassMemberKind.Field,
                                    new DelphiField(
                                        fieldName,
                                        fieldType)));

                            continue;
                        }

                        // Unknown class declaration.
                        // Skip until ';' so the parser stays synchronized.
                        while (!IsEnd() &&
                               !PeekSymbol(";"))
                        {
                            Advance();
                        }

                        MatchSymbol(";");

                        continue;
                    }

                    // --------------------------------------------
                    // Unknown class member
                    // --------------------------------------------

                    Advance();
                }

                declarations.Add(
                    new DelphiClass(
                        name,
                        baseType,
                        members));

                continue;
            }

            // ------------------------------------------------
            // INTERFACE
            // ------------------------------------------------

            if (MatchKeyword("interface"))
            {
                var members =
                    new List<DelphiClassMember>();

                while (!IsEnd() &&
                       !PeekKeyword("end"))
                {
                    // ------------------------------------------------
                    // Method
                    // ------------------------------------------------

                    if (MatchKeyword("procedure"))
                    {
                        var function =
                            ParseFunction(true);

                        members.Add(
                            new DelphiClassMember(
                                DelphiVisibility.Public,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }

                    if (MatchKeyword("function"))
                    {
                        var function =
                            ParseFunction(false);

                        members.Add(
                            new DelphiClassMember(
                                DelphiVisibility.Public,
                                DelphiClassMemberKind.Method,
                                function));

                        continue;
                    }

                    // ------------------------------------------------
                    // Property
                    // ------------------------------------------------

                    if (MatchKeyword("property"))
                    {
                        // Пока сохраняем property как неизвестный
                        // член интерфейса. Полноценный property parser
                        // сделаем отдельным шагом.

                        ReadUntil(";");

                        MatchSymbol(";");

                        continue;
                    }

                    Advance();
                }

                ExpectKeyword("end");
                MatchSymbol(";");

                declarations.Add(
                    new DelphiInterface(
                        name,
                        null,
                        members));

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
               ReadQualifiedIdentifier();

        var parameters =
            new List<DelphiParameter>();

        if (MatchSymbol("("))
        {
            while (!IsEnd() &&
               !PeekSymbol(")"))
            {
                var parameterKind =
                    DelphiParameterKind.Value;

                if (MatchKeyword("const"))
                {
                    parameterKind = DelphiParameterKind.Const;
                }
                else if (MatchKeyword("var"))
                {
                    parameterKind = DelphiParameterKind.Var;
                }
                else if (MatchKeyword("out"))
                {
                    parameterKind = DelphiParameterKind.Out;
                }

                var parameterNames = new List<string>();

                // Первое имя
                parameterNames.Add(
                    ExpectIdentifier());

                // Дополнительные имена:
                //
                // const A, B, C: Integer
                //
                while (MatchSymbol(","))
                {
                    parameterNames.Add(
                        ExpectIdentifier());
                }

                string type = "";

                if (MatchSymbol(":"))
                {
                    type = ReadParameterType();
                }

                // Если тип указан после группы,
                // применяем его ко всем именам.
                foreach (string parameterName in parameterNames)
                {
                    parameters.Add(
                        new DelphiParameter(
                            parameterName,
                            type,
                            parameterKind));
                }

                MatchSymbol(";");
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

    private Exception ParserError(string message)
    {
        string current =
            IsEnd()
                ? "<EOF>"
                : $"{Current().Kind} '{Current().Text}'";

        string previous = "<none>";

        if (_position > 0)
        {
            var token = _tokens[_position - 1];

            previous =
                $"{token.Kind} '{token.Text}'";
        }

        string next = "<none>";

        if (_position + 1 < _tokens.Count)
        {
            var token = _tokens[_position + 1];

            next =
                $"{token.Kind} '{token.Text}'";
        }

        return new Exception(
            $"{message}\n" +
            $"  Position: {_position}\n" +
            $"  Previous: {previous}\n" +
            $"  Current:  {current}\n" +
            $"  Next:     {next}");
    }

    private string ReadQualifiedIdentifier()
    {
        string name = ExpectIdentifier();

        while (MatchSymbol("."))
        {
            name += ".";
            name += ExpectIdentifier();
        }

        return name;
    }

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
            throw ParserError(
                  $"Expected symbol '{value}'");
        }
    }

    private string ExpectTypeName()
    {
        if (IsEnd())
            throw ParserError(
                  $"Expected type name, got '{Current().Text}'");

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

    private string ReadParameterType()
    {
        var parts = new List<string>();

        int squareDepth = 0;
        int parenDepth = 0;

        while (!IsEnd())
        {
            if (PeekSymbol("["))
            {
                squareDepth++;
            }
            else if (PeekSymbol("]"))
            {
                squareDepth--;
            }
            else if (PeekSymbol("("))
            {
                parenDepth++;
            }
            else if (PeekSymbol(")"))
            {
                if (squareDepth == 0 &&
                    parenDepth == 0)
                {
                    break;
                }

                parenDepth--;
            }

            if (squareDepth == 0 &&
                parenDepth == 0 &&
                (PeekSymbol(";") ||
                 PeekSymbol(",") ||
                 PeekSymbol(")")))
            {
                break;
            }

            parts.Add(Current().Text);
            Advance();
        }

        return string.Join(" ", parts);
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
            throw ParserError(
                  $"Expected type name, got '{Current().Text}'");
        }

        string value = Current().Text;

        Advance();

        return value;


    }
}