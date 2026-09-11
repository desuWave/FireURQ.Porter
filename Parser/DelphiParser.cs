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

        string name =
            ExpectIdentifier();

        ExpectSymbol(";");

        var uses =
            new List<string>();

        var declarations =
            new List<DelphiNode>();

        // ----------------------------------------------------
        // INTERFACE SECTION
        // ----------------------------------------------------

        if (MatchKeyword("interface"))
        {
            uses.AddRange(ParseUses());

            ParseDeclarationSection(
                declarations,
                readBodies: false,
                stopAtImplementation: true);
        }
        else
        {
            uses.AddRange(ParseUses());

            ParseDeclarationSection(
                declarations,
                readBodies: false,
                stopAtImplementation: true);
        }

        // ----------------------------------------------------
        // IMPLEMENTATION SECTION
        // ----------------------------------------------------

        if (MatchKeyword("implementation"))
        {
            Console.WriteLine(
                $"[UNIT-IMPLEMENTATION] entered pos={_position}");

            Console.WriteLine(
                $"[UNIT-IMPLEMENTATION-BEFORE-USES] " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            int usesBefore = _position;

            uses.AddRange(ParseUses());

            Console.WriteLine(
                $"[UNIT-IMPLEMENTATION-AFTER-USES] " +
                $"before={usesBefore} " +
                $"after={_position}");

            Console.WriteLine(
                $"[UNIT-IMPLEMENTATION-NEXT] " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            ParseDeclarationSection(
                declarations,
                readBodies: true,
                stopAtImplementation: false);
        }

        return new DelphiUnit(
            name,
            uses,
            declarations);
    }

    // ========================================================
    // DECLARATION SECTION
    // ========================================================

    private void ParseDeclarationSection(
        List<DelphiNode> declarations,
        bool readBodies,
        bool stopAtImplementation)
    {
        if (readBodies)
        {
            Console.WriteLine(
                $"[PARSER-IMPL-START] position={_position}");
        }

        while (!IsEnd())
        {
            Console.WriteLine(
                $"[DECL] pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            if (stopAtImplementation &&
                PeekKeyword("implementation"))
            {
                Console.WriteLine(
                    $"[DECL-STOP-IMPLEMENTATION] pos={_position}");

                return;
            }

            if (readBodies &&
                (PeekKeyword("function") ||
                 PeekKeyword("procedure") ||
                 PeekKeyword("constructor") ||
                 PeekKeyword("destructor")))
            {
                Console.WriteLine(
                    $"[PARSER-CALLABLE] " +
                    $"{Current().Text} " +
                    $"position={_position} " +
                    $"line={Current().Line}");

                Console.Write("  tokens: ");

                for (int j = 0;
                     j < 12 &&
                     _position + j < _tokens.Count;
                     j++)
                {
                    Console.Write(
                        $"{_tokens[_position + j].Text} ");
                }

                Console.WriteLine();
            }

            if (MatchKeyword("const"))
            {
                int before = _position - 1;

                Console.WriteLine(
                    $"[DECL-CONST-BEGIN] pos={before}");

                ParseConstSection(declarations);

                Console.WriteLine(
                    $"[DECL-CONST-END] pos={_position}");

                continue;
            }

            if (MatchKeyword("type"))
            {
                int before = _position - 1;

                Console.WriteLine(
                    $"[DECL-TYPE-BEGIN] pos={before}");

                ParseTypeSection(declarations);

                Console.WriteLine(
                    $"[DECL-TYPE-END] pos={_position}");

                continue;
            }

            if (PeekKeyword("function") ||
                PeekKeyword("procedure"))
            {
                int before = _position;

                Console.WriteLine(
                    $"[DECL-CALLABLE-BEGIN] " +
                    $"pos={before} " +
                    $"line={Current().Line}");

                DelphiNode callable =
                    ParseCallableDeclaration(
                        readBodies);

                Console.WriteLine(
                    $"[DECL-CALLABLE-END] " +
                    $"before={before} " +
                    $"after={_position}");

                declarations.Add(callable);
                continue;
            }

            if (MatchKeyword("end"))
            {
                Console.WriteLine(
                    $"[DECL-END] pos={_position - 1}");

                MatchSymbol(".");

                break;
            }

            Advance();
        }
    }

    // ========================================================
    // CALLABLE DECLARATION
    // ========================================================

    private DelphiNode ParseCallableDeclaration(
        bool readBody)
    {
        bool isProcedure =
            PeekKeyword("procedure");

        if (!isProcedure &&
            !PeekKeyword("function"))
        {
            throw ParserError(
                "Expected function or procedure.");
        }

        // Delphi method resolution:
        //
        // function A.B = C;
        // procedure A.B = C;
        //
        // Recognize it before ParseFunction(),
        // because it has no normal return-type syntax.

        if (IsMethodResolutionAhead())
        {
            Advance();

            string interfaceMethod =
                ReadQualifiedIdentifier();

            ExpectSymbol("=");

            string implementationMethod =
                ReadQualifiedIdentifier();

            ExpectSymbol(";");

            return new DelphiMethodResolution(
                interfaceMethod,
                implementationMethod);
        }

        Advance();

        return ParseFunction(
            isProcedure,
            readBody);
    }

    // ========================================================
    // METHOD RESOLUTION LOOKAHEAD
    // ========================================================

    private bool IsMethodResolutionAhead()
    {
        int position =
            _position + 1;

        if (!IsIdentifierAt(position))
        {
            return false;
        }

        position++;

        while (position + 1 < _tokens.Count &&
               IsSymbolAt(position, ".") &&
               IsIdentifierAt(position + 1))
        {
            position += 2;
        }

        return IsSymbolAt(
            position,
            "=");
    }

    private bool IsKeywordAt(int offset)
    {
        int position =
            _position + offset;

        if (position < 0 ||
            position >= _tokens.Count)
        {
            return false;
        }

        return _tokens[position].Kind ==
               TokenKind.Keyword;
    }

    private bool IsKeywordAt(
        int offset,
        string keyword)
    {
        int position =
            _position + offset;

        if (position < 0 ||
            position >= _tokens.Count)
        {
            return false;
        }

        return _tokens[position].Kind ==
                   TokenKind.Keyword &&
               _tokens[position].Text.Equals(
                   keyword,
                   StringComparison.OrdinalIgnoreCase);
    }

    private bool IsIdentifierAt(int position)
    {
        if (position < 0 ||
            position >= _tokens.Count)
        {
            return false;
        }

        return _tokens[position].Kind ==
                   TokenKind.Identifier ||
               _tokens[position].Kind ==
                   TokenKind.Keyword;
    }

    private bool IsSymbolAt(
        int position,
        string symbol)
    {
        if (position < 0 ||
            position >= _tokens.Count)
        {
            return false;
        }

        return _tokens[position].Kind ==
                   TokenKind.Symbol &&
               _tokens[position].Text == symbol;
    }

    // ========================================================
    // USES
    // ========================================================

    private List<string> ParseUses()
    {
        var result =
            new List<string>();

        if (!MatchKeyword("uses"))
        {
            return result;
        }

        while (!IsEnd())
        {
            result.Add(
                ReadQualifiedIdentifier());

            if (MatchSymbol(";"))
            {
                break;
            }

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
            Console.WriteLine(
                $"[CONST] pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            if (PeekKeyword("type") ||
                PeekKeyword("var") ||
                PeekKeyword("function") ||
                PeekKeyword("procedure") ||
                PeekKeyword("implementation"))
            {
                Console.WriteLine(
                    $"[CONST-STOP] pos={_position} " +
                    $"text={Current().Text}");

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
                type =
                    ReadTypeExpression();
            }

            if (!MatchSymbol("="))
            {
                continue;
            }

            string value =
                ReadUntilStatementTerminator();

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
            {
                continue;
            }

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
                        string.Join(
                            ", ",
                            values)));

                continue;
            }

            // ------------------------------------------------
            // POINTER
            // ------------------------------------------------

            if (MatchSymbol("^"))
            {
                string target =
                    ExpectTypeName();

                MatchSymbol(";");

                declarations.Add(
                    new DelphiType(
                        name,
                        DelphiTypeKind.Pointer,
                        target));

                continue;
            }

            // ------------------------------------------------
            // ARRAY
            // ------------------------------------------------

            if (MatchKeyword("array"))
            {
                var definition =
                    new List<string>();

                definition.Add("array");

                if (MatchSymbol("["))
                {
                    definition.Add("[");

                    while (!IsEnd() &&
                           !PeekSymbol("]"))
                    {
                        definition.Add(
                            Current().Text);

                        Advance();
                    }

                    ExpectSymbol("]");

                    definition.Add("]");
                }

                if (MatchKeyword("of"))
                {
                    definition.Add("of");

                    definition.Add(
                        ExpectTypeName());
                }

                MatchSymbol(";");

                declarations.Add(
                    new DelphiType(
                        name,
                        DelphiTypeKind.Array,
                        string.Join(
                            " ",
                            definition)));

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

                if (MatchSymbol("("))
                {
                    baseType =
                        ReadQualifiedIdentifier();

                    while (MatchSymbol(","))
                    {
                        ReadQualifiedIdentifier();
                    }

                    ExpectSymbol(")");
                }

                // Forward declaration:
                //
                // TFoo = class;
                //

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

                while (!IsEnd())
                {
                    // ----------------------------------------
                    // END OF CLASS
                    // ----------------------------------------

                    if (PeekKeyword("end"))
                    {
                        Advance();
                        MatchSymbol(";");
                        break;
                    }

                    // ----------------------------------------
                    // VISIBILITY
                    // ----------------------------------------

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

                    // ----------------------------------------
                    // PROPERTY
                    // ----------------------------------------

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

                    // ----------------------------------------
                    // CLASS FUNCTION / PROCEDURE
                    // ----------------------------------------
                    //
                    // Delphi:
                    //
                    // class function Make: IFoo;
                    // class procedure DoSomething;
                    //
                    // At this point the current token is
                    // "class". Consume it, then parse the
                    // following function/procedure normally.
                    //

                    if (PeekKeyword("class") &&
                        (IsKeywordAt(1, "function") ||
                         IsKeywordAt(1, "procedure")))
                    {
                        Advance(); // class

                        bool isProcedure =
                            PeekKeyword("procedure");

                        Advance(); // function / procedure

                        var classFunction =
                            ParseFunction(
                                isProcedure,
                                readBody: false);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                classFunction));

                        continue;
                    }

                    // ----------------------------------------
                    // FUNCTION / PROCEDURE
                    // ----------------------------------------

                    if (PeekKeyword("function") ||
                        PeekKeyword("procedure"))
                    {
                        bool isProcedure =
                            PeekKeyword("procedure");

                        // Class method resolution:
                        //
                        // function IFoo.GetByID = GetPicture;
                        //
                        // Keep this before ordinary
                        // ParseFunction().

                        if (IsMethodResolutionAhead())
                        {
                            Advance();

                            string interfaceMethod =
                                ReadQualifiedIdentifier();

                            ExpectSymbol("=");

                            string implementationMethod =
                                ReadQualifiedIdentifier();

                            ExpectSymbol(";");

                            members.Add(
                                new DelphiClassMember(
                                    visibility,
                                    DelphiClassMemberKind.MethodResolution,
                                    new DelphiMethodResolution(
                                        interfaceMethod,
                                        implementationMethod)));

                            continue;
                        }

                        Advance();

                        var classFunction =
                            ParseFunction(
                                isProcedure,
                                readBody: false);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                classFunction));

                        continue;
                    }

                    // ----------------------------------------
                    // CONSTRUCTOR
                    // ----------------------------------------

                    if (MatchKeyword("constructor"))
                    {
                        var constructor =
                            ParseFunction(
                                isProcedure: true,
                                readBody: false);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                constructor));

                        continue;
                    }

                    // ----------------------------------------
                    // DESTRUCTOR
                    // ----------------------------------------

                    if (MatchKeyword("destructor"))
                    {
                        var destructor =
                            ParseFunction(
                                isProcedure: true,
                                readBody: false);

                        members.Add(
                            new DelphiClassMember(
                                visibility,
                                DelphiClassMemberKind.Method,
                                destructor));

                        continue;
                    }

                    // ----------------------------------------
                    // FIELD
                    // ----------------------------------------

                    if (IsIdentifier())
                    {
                        string fieldName =
                            ExpectIdentifier();

                        if (MatchSymbol(":"))
                        {
                            string fieldType =
                                ReadTypeExpression();

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
                    }

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
            // RECORD
            // ------------------------------------------------

            if (MatchKeyword("record"))
            {
                var definition =
                    new List<string>();

                int depth = 1;

                while (!IsEnd() &&
                       depth > 0)
                {
                    if (PeekKeyword("record"))
                    {
                        depth++;
                    }
                    else if (PeekKeyword("end"))
                    {
                        depth--;

                        if (depth == 0)
                        {
                            Advance();
                            break;
                        }
                    }

                    if (depth > 0)
                    {
                        definition.Add(
                            Current().Text);

                        Advance();
                    }
                }

                MatchSymbol(";");

                declarations.Add(
                    new DelphiType(
                        name,
                        DelphiTypeKind.Record,
                        string.Join(
                            " ",
                            definition)));

                continue;
            }

            // ------------------------------------------------
            // PROCEDURE TYPE
            // ------------------------------------------------

            if (MatchKeyword("procedure"))
            {
                string definition =
                    ReadUntil(";");

                MatchSymbol(";");

                declarations.Add(
                    new DelphiType(
                        name,
                        DelphiTypeKind.Procedure,
                        $"procedure {definition}"));

                continue;
            }

            // ------------------------------------------------
            // FUNCTION TYPE
            // ------------------------------------------------

            if (MatchKeyword("function"))
            {
                string definition =
                    ReadUntil(";");

                MatchSymbol(";");

                declarations.Add(
                    new DelphiType(
                        name,
                        DelphiTypeKind.Procedure,
                        $"function {definition}"));

                continue;
            }

            // ------------------------------------------------
            // UNKNOWN TYPE
            // ------------------------------------------------

            var unknownDefinition =
                new List<string>();

            while (!IsEnd() &&
                   !PeekSymbol(";"))
            {
                unknownDefinition.Add(
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
                        unknownDefinition)));
        }
    }

    // ========================================================
    // PROPERTY
    // ========================================================

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
                {
                    parameterKind =
                        DelphiParameterKind.Const;
                }
                else if (MatchKeyword("var"))
                {
                    parameterKind =
                        DelphiParameterKind.Var;
                }
                else if (MatchKeyword("out"))
                {
                    parameterKind =
                        DelphiParameterKind.Out;
                }

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
                            parameterKind,
                            null));
                }

                MatchSymbol(";");
            }

            ExpectSymbol("]");
        }

        string type = "";

        if (MatchSymbol(":"))
        {
            type =
                ReadPropertyType();
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

    private DelphiFunction ParseFunction(
        bool isProcedure,
        bool readBody)
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
                    parameterKind =
                        DelphiParameterKind.Const;
                }
                else if (MatchKeyword("var"))
                {
                    parameterKind =
                        DelphiParameterKind.Var;
                }
                else if (MatchKeyword("out"))
                {
                    parameterKind =
                        DelphiParameterKind.Out;
                }

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

                string? defaultValue = null;

                if (MatchSymbol("="))
                {
                    defaultValue =
                        ReadParameterDefaultValue();
                }

                foreach (string parameterName in parameterNames)
                {
                    parameters.Add(
                        new DelphiParameter(
                            parameterName,
                            parameterType,
                            parameterKind,
                            defaultValue));
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

        IReadOnlyList<LexerToken> bodyTokens =
            Array.Empty<LexerToken>();

        if (readBody)
        {
            SkipLocalDeclarations();

            Console.WriteLine(
                $"[CALLABLE-BODY] {name} " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            bodyTokens =
                ReadFunctionBody();

            Console.WriteLine(
                $"[AFTER-READ-BODY] {name} " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");

            Console.WriteLine(
                $"[CALLABLE-BODY-END] {name} " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text} " +
                $"bodyTokens={bodyTokens.Count}");

            Console.WriteLine(
                $"[PARSE-FUNCTION-END] {name} " +
                $"pos={_position} " +
                $"line={Current().Line} " +
                $"kind={Current().Kind} " +
                $"text={Current().Text}");
        }

        return new DelphiFunction(
            name,
            parameters,
            returnType,
            isProcedure,
            bodyTokens);
    }

    // ========================================================
    // LOCAL DECLARATIONS
    // ========================================================

    private void SkipLocalDeclarations()
    {
        while (!IsEnd())
        {
            if (PeekKeyword("var") ||
                PeekKeyword("const") ||
                PeekKeyword("type"))
            {
                Advance();

                SkipLocalDeclarationSection();

                continue;
            }

            if (PeekKeyword("function") ||
                PeekKeyword("procedure"))
            {
                SkipLocalCallableDeclaration();

                continue;
            }

            break;
        }
    }

    private void SkipLocalDeclarationSection()
    {
        while (!IsEnd() &&
               !PeekKeyword("begin") &&
               !PeekKeyword("function") &&
               !PeekKeyword("procedure"))
        {
            Advance();
        }
    }

    private void SkipLocalCallableDeclaration()
    {
        // Skip "function" / "procedure".
        Advance();

        // Skip the callable header until its terminating semicolon.
        // This includes:
        //
        // function BoolToVar(
        //   const aBool: Boolean
        // ): Variant;
        //
        // We deliberately do not try to parse the header here.
        while (!IsEnd())
        {
            if (MatchSymbol(";"))
            {
                break;
            }

            Advance();
        }

        // A local callable with no body is possible in some
        // declaration forms. If the next token is not "begin",
        // leave it for the surrounding declaration scanner.
        if (!PeekKeyword("begin"))
        {
            return;
        }

        // Skip the complete local callable body.
        //
        // We balance begin/end so that nested blocks and nested
        // begin/end pairs do not terminate the scan prematurely.
        int beginDepth = 0;

        while (!IsEnd())
        {
            if (PeekKeyword("begin"))
            {
                beginDepth++;
                Advance();
                continue;
            }

            if (PeekKeyword("end"))
            {
                Advance();

                beginDepth--;

                MatchSymbol(";");

                if (beginDepth == 0)
                {
                    break;
                }

                continue;
            }

            Advance();
        }
    }

    // ========================================================
    // FUNCTION BODY
    // ========================================================

    private IReadOnlyList<LexerToken> ReadFunctionBody()
    {

        Console.WriteLine("=== GetVirtualVariable BODY WINDOW ===");

        for (int i = _position;
             i < Math.Min(_tokens.Count, _position + 40);
             i++)
        {
            var t = _tokens[i];

            Console.WriteLine(
                $"[BODY-TOK] pos={i} " +
                $"line={t.Line} " +
                $"col={t.Column} " +
                $"kind={t.Kind} " +
                $"text={t.Text}");
        }

        Console.WriteLine("=== END BODY WINDOW ===");

        var body = new List<LexerToken>();

        if (!PeekKeyword("begin"))
            return body;

        int beginDepth = 0;
        int caseDepth = 0;

        while (!IsEnd())
        {
            if (PeekKeyword("begin"))
            {
                beginDepth++;

                Console.WriteLine(
                    $"[BODY-BEGIN] pos={_position} " +
                    $"line={Current().Line} " +
                    $"beginDepth={beginDepth} " +
                    $"caseDepth={caseDepth}");

                body.Add(Current());
                Advance();
                continue;
            }

            if (PeekKeyword("case"))
            {
                caseDepth++;

                Console.WriteLine(
                    $"[BODY-CASE] pos={_position} " +
                    $"line={Current().Line} " +
                    $"beginDepth={beginDepth} " +
                    $"caseDepth={caseDepth}");

                body.Add(Current());
                Advance();
                continue;
            }

            if (PeekKeyword("end"))
            {
                Console.WriteLine(
                    $"[BODY-END] pos={_position} " +
                    $"line={Current().Line} " +
                    $"beginDepth={beginDepth} " +
                    $"caseDepth={caseDepth}");

                body.Add(Current());
                Advance();

                if (caseDepth > 0)
                {
                    caseDepth--;

                    Console.WriteLine(
                        $"[BODY-CASE-END] " +
                        $"beginDepth={beginDepth} " +
                        $"caseDepth={caseDepth}");

                    MatchSymbol(";");
                    continue;
                }

                beginDepth--;

                Console.WriteLine(
                    $"[BODY-BEGIN-END] " +
                    $"beginDepth={beginDepth} " +
                    $"caseDepth={caseDepth}");

                Console.WriteLine(
                    $"[BODY-AFTER-END] pos={_position} " +
                    $"line={Current().Line} " +
                    $"kind={Current().Kind} " +
                    $"text={Current().Text}");

                if (beginDepth == 0)
                {
                    MatchSymbol(";");

                    Console.WriteLine(
                        $"[BODY-BREAK] pos={_position} " +
                        $"line={Current().Line} " +
                        $"kind={Current().Kind} " +
                        $"text={Current().Text}");

                    break;
                }

                MatchSymbol(";");
                continue;
            }

            body.Add(Current());
            Advance();
        }

        return body;
    }

    // ========================================================
    // TYPE HELPERS
    // ========================================================

    private string ReadPropertyType()
    {
        var parts =
            new List<string>();

        while (!IsEnd())
        {
            if (PeekKeyword("read") ||
                PeekKeyword("write") ||
                PeekKeyword("default") ||
                PeekSymbol(";"))
            {
                break;
            }

            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private string ReadParameterType()
    {
        var parts =
            new List<string>();

        while (!IsEnd())
        {
            if (PeekSymbol("=") ||
                PeekSymbol(";") ||
                PeekSymbol(")") ||
                PeekSymbol("]") ||
                PeekSymbol(","))
            {
                break;
            }

            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private string ReadTypeExpression()
    {
        var parts =
            new List<string>();

        while (!IsEnd())
        {
            if (PeekSymbol("=") ||
                PeekSymbol(";"))
            {
                break;
            }

            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private string ReadParameterDefaultValue()
    {
        var parts =
            new List<string>();

        int roundDepth = 0;
        int squareDepth = 0;
        int curlyDepth = 0;

        while (!IsEnd())
        {
            if (PeekSymbol("("))
            {
                roundDepth++;
            }
            else if (PeekSymbol(")"))
            {
                if (roundDepth == 0 &&
                    squareDepth == 0 &&
                    curlyDepth == 0)
                {
                    break;
                }

                if (roundDepth > 0)
                {
                    roundDepth--;
                }
            }
            else if (PeekSymbol("["))
            {
                squareDepth++;
            }
            else if (PeekSymbol("]"))
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
                }
            }
            else if (PeekSymbol("{"))
            {
                curlyDepth++;
            }
            else if (PeekSymbol("}"))
            {
                if (curlyDepth > 0)
                {
                    curlyDepth--;
                }
            }
            else if (PeekSymbol(";") &&
                     roundDepth == 0 &&
                     squareDepth == 0 &&
                     curlyDepth == 0)
            {
                break;
            }

            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private string ExpectTypeName()
    {
        return ReadQualifiedIdentifier();
    }

    // ========================================================
    // IDENTIFIERS
    // ========================================================

    private string ReadQualifiedIdentifier()
    {
        string result =
            ExpectIdentifier();

        while (MatchSymbol("."))
        {
            result += ".";
            result += ExpectIdentifier();
        }

        return result;
    }

    private string ExpectIdentifier()
    {
        if (!IsIdentifier())
        {
            throw ParserError(
                "Expected identifier.");
        }

        string text =
            Current().Text;

        Advance();

        return text;
    }

    // ========================================================
    // TOKEN HELPERS
    // ========================================================

    private string ReadUntil(
        string symbol)
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

    private string ReadUntilStatementTerminator()
    {
        var parts =
            new List<string>();

        int roundDepth = 0;
        int squareDepth = 0;
        int curlyDepth = 0;

        while (!IsEnd())
        {
            if (PeekSymbol("("))
            {
                roundDepth++;
            }
            else if (PeekSymbol(")"))
            {
                if (roundDepth > 0)
                {
                    roundDepth--;
                }
            }
            else if (PeekSymbol("["))
            {
                squareDepth++;
            }
            else if (PeekSymbol("]"))
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
                }
            }
            else if (PeekSymbol("{"))
            {
                curlyDepth++;
            }
            else if (PeekSymbol("}"))
            {
                if (curlyDepth > 0)
                {
                    curlyDepth--;
                }
            }
            else if (PeekSymbol(";") &&
                     roundDepth == 0 &&
                     squareDepth == 0 &&
                     curlyDepth == 0)
            {
                break;
            }

            parts.Add(
                Current().Text);

            Advance();
        }

        return string.Join(
            " ",
            parts);
    }

    private LexerToken Current()
    {
        if (_position >= _tokens.Count)
        {
            return _tokens[^1];
        }

        return _tokens[_position];
    }

    private bool IsEnd()
    {
        return Current().Kind ==
               TokenKind.EndOfFile;
    }

    private bool IsIdentifier()
    {
        return Current().Kind ==
                   TokenKind.Identifier ||
               Current().Kind ==
                   TokenKind.Keyword;
    }

    private bool PeekKeyword(
        string keyword)
    {
        return Current().Kind ==
                   TokenKind.Keyword &&
               Current().Text.Equals(
                   keyword,
                   StringComparison.OrdinalIgnoreCase);
    }

    private bool PeekSymbol(
        string symbol)
    {
        return Current().Kind ==
                   TokenKind.Symbol &&
               Current().Text == symbol;
    }

    private bool MatchKeyword(
        string keyword)
    {
        if (!PeekKeyword(keyword))
        {
            return false;
        }

        Advance();

        return true;
    }

    private bool MatchSymbol(
        string symbol)
    {
        if (!PeekSymbol(symbol))
        {
            return false;
        }

        Advance();

        return true;
    }

    private void ExpectKeyword(
        string keyword)
    {
        if (!MatchKeyword(keyword))
        {
            throw ParserError(
                $"Expected keyword '{keyword}'.");
        }
    }

    private void ExpectSymbol(
        string symbol)
    {
        if (!MatchSymbol(symbol))
        {
            throw ParserError(
                $"Expected symbol '{symbol}'.");
        }
    }


    private void Advance()
    {
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }
    }



    // ========================================================
    // ERROR DIAGNOSTICS
    // ========================================================

    private Exception ParserError(
        string message)
    {
        string current =
            IsEnd()
                ? "<EOF>"
                : $"{Current().Kind} '{Current().Text}'";

        var context =
            new List<string>();

        const int radius = 8;

        int start =
            Math.Max(
                0,
                _position - radius);

        int end =
            Math.Min(
                _tokens.Count,
                _position + radius + 1);

        for (int i = start; i < end; i++)
        {
            var token =
                _tokens[i];

            string marker =
                i == _position
                    ? ">"
                    : " ";

            context.Add(
                $"{marker} {i,5}: " +
                $"{token.Kind} '{token.Text}'");
        }

        return new InvalidOperationException(
            $"{message} Current token: {current}. " +
            $"Position: {_position}." +
            Environment.NewLine +
            "Token context:" +
            Environment.NewLine +
            string.Join(
                Environment.NewLine,
                context));
    }
}
