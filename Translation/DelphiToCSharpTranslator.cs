using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class DelphiToCSharpTranslator
{
    private readonly TypeResolver? _resolver;
    private readonly string? _unitName;

    public DelphiToCSharpTranslator(
        TypeResolver? resolver = null,
        string? unitName = null)
    {
        _resolver = resolver;
        _unitName = unitName;
    }

    private bool IsBooleanType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && MapType(type) == "bool";

    private string MapType(string? type) =>
        string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(type?.Trim(), "Pd2dRenderTarget", StringComparison.OrdinalIgnoreCase)
            ? "Td2dRenderTarget"
            : CSharpTypeMapper.Map(type, _resolver, _unitName);

    private FunctionTypeContext? _currentTypeContext;

    public string TranslateFunction(
        SemanticFunction function,
        BehaviorAnalyzer.BehaviorInfo behavior,
        bool manageDestructorLifetime = false) =>
        TranslateFunctionCore(function, behavior, manageDestructorLifetime, null);

    private string TranslateFunctionCore(
        SemanticFunction function, BehaviorAnalyzer.BehaviorInfo behavior,
        bool manageDestructorLifetime, FunctionTypeContext? parent)
    {
        FunctionTypeContext? previous =
            _currentTypeContext;

        _currentTypeContext =
            new FunctionTypeContext(function, parent);
        FunctionTypeContext context = _currentTypeContext;

        try
        {
            var builder = new StringBuilder();

            string returnType =
                function.IsProcedure
                    ? "void"
                    : MapType(function.ReturnType);

            builder
                .Append(returnType)
                .Append(' ')
                .Append(function.Name)
                .Append('(');

            for (int i = 0; i < function.Parameters.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                SemanticParameter parameter =
                    function.Parameters[i];

                bool isLogArgumentArray =
                    i == 1 &&
                    string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                    function.IsProcedure &&
                    function.Name.Equals("System_Log", StringComparison.OrdinalIgnoreCase) &&
                    function.Parameters.Count == 2 &&
                    MapType(function.Parameters[0].Type) == "string" &&
                    MapType(parameter.Type) == "TVarRec[]";

                builder
                    .Append(MapParameterKind(parameter.Kind))
                    .Append(isLogArgumentArray ? "object[]" : MapType(parameter.Type))
                    .Append(' ')
                    .Append(parameter.Name)
                    // Preserve the RenderBatch default.
                    .Append(string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                            function.Name.Equals("RenderBatch", StringComparison.OrdinalIgnoreCase) &&
                            function.Parameters.Count() == 1 &&
                            parameter.Name.Equals("aEndScene", StringComparison.OrdinalIgnoreCase) &&
                            MapType(parameter.Type) == "bool"
                        ? " = false"
                        : string.Empty);

                // Preserve the defaults of Td2dCore render-target calls.
                if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase))
                {
                    if (function.Name.Equals("Gfx_BeginScene", StringComparison.OrdinalIgnoreCase) &&
                        function.Parameters.Count() == 1 &&
                        parameter.Name.Equals("aTarget", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append(" = default");
                    }
                    else if (function.Name.Equals("Target_Create", StringComparison.OrdinalIgnoreCase) &&
                             i == 2 && function.Parameters.Count() == 3 &&
                             parameter.Name.Equals("aZBuffer", StringComparison.OrdinalIgnoreCase) &&
                             MapType(parameter.Type) == "bool")
                    {
                        builder.Append(" = false");
                    }
                }

                // Preserve the defaults of Td2dCore.Texture_Load.
                if (i > 0 &&
                    string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                    function.Name.Equals("Texture_Load", StringComparison.OrdinalIgnoreCase) &&
                    function.Parameters.Select(p => p.Name).SequenceEqual(
                        new[] { "aFileName", "aPackOnly", "aUseCache", "aMipMap", "aPicSize" },
                        StringComparer.OrdinalIgnoreCase) &&
                    function.Parameters.Select(p => MapType(p.Type)).SequenceEqual(
                        new[] { "string", "bool", "bool", "bool", "IntPtr" }))
                {
                    builder.Append(i switch
                    {
                        2 => " = true",
                        4 => " = default",
                        _ => " = false"
                    });
                }
            }

            builder.AppendLine(")");
            builder.AppendLine("{");
            foreach (var copy in context.ConstCopies)
                builder.AppendLine($"    var {copy.Value} = {copy.Key};");

            if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                function.Name.Equals("System_Log", StringComparison.OrdinalIgnoreCase) &&
                function.IsProcedure && !function.IsClassMethod &&
                function.Parameters.Count == 1 &&
                function.Parameters[0].Name.Equals("aString", StringComparison.OrdinalIgnoreCase) &&
                function.Parameters[0].Kind.Equals("const", StringComparison.OrdinalIgnoreCase) &&
                MapType(function.Parameters[0].Type) == "string")
            {
                builder.AppendLine(
                    $"    global::FireURQ.Runtime.D2dLogFormat.AppendLine(f_LogFileName, {function.Parameters[0].Name});");
                builder.AppendLine("}");
                return builder.ToString();
            }

            if (string.Equals(_unitName, "f2ScnMain", StringComparison.OrdinalIgnoreCase) &&
                function.Name.Equals("PutTextAsUnicode", StringComparison.OrdinalIgnoreCase) &&
                function.IsProcedure && !function.IsClassMethod &&
                function.Parameters.Count == 1 && MapType(function.Parameters[0].Type) == "string")
            {
                builder.AppendLine($"    SetUnicodeText({function.Parameters[0].Name});");
                builder.AppendLine("}");
                return builder.ToString();
            }

            if (manageDestructorLifetime && function.IsDestructor)
            {
                string scopeName = "__fireurqDestructorScope";
                while (function.LocalVariables.Any(variable =>
                           variable.Name.Equals(scopeName,
                               StringComparison.OrdinalIgnoreCase)) ||
                       function.Parameters.Any(parameter =>
                           parameter.Name.Equals(scopeName,
                               StringComparison.OrdinalIgnoreCase)))
                    scopeName += "_";

                builder.AppendLine(
                    $"    using var {scopeName} = __FireUrqEnterDestructor();");
            }

            foreach (
                SemanticVariable variable
                in function.LocalVariables)
            {
                if (variable.AbsoluteTarget is not null &&
                    string.Equals(_unitName, "f2ScnMain",
                        StringComparison.OrdinalIgnoreCase) &&
                    function.Name.Equals("CompareDecorSprites",
                        StringComparison.OrdinalIgnoreCase) &&
                    MapType(variable.Type) == "Tf2DecorationSprite" &&
                    function.Parameters.Any(parameter =>
                        parameter.Name.Equals(variable.AbsoluteTarget,
                            StringComparison.OrdinalIgnoreCase) &&
                        MapType(parameter.Type) == "IntPtr" &&
                        parameter.Kind.Equals("Value",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    // This comparator only reads the aliased object references.
                    // TObjectList.Sort must supply GCHandle tokens for this call.
                    string target = function.Parameters.First(parameter =>
                        parameter.Name.Equals(variable.AbsoluteTarget,
                            StringComparison.OrdinalIgnoreCase)).Name;

                    builder.AppendLine(
                        $"    {MapType(variable.Type)} {variable.Name} = " +
                        $"({MapType(variable.Type)})global::System.Runtime.InteropServices." +
                        $"GCHandle.FromIntPtr({target}).Target!;");
                }
                else
                {
                    builder.AppendLine(
                        $"    {MapType(variable.Type)} " +
                        $"{variable.Name}" +
                        (MapType(variable.Type) == "AnsiString" ||
                         (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                          variable.Name.Equals("l_DevInfo", StringComparison.OrdinalIgnoreCase) &&
                          MapType(variable.Type) == "BASS_DEVICEINFO") ||
                         (string.Equals(_unitName, "f2PicturePool", StringComparison.OrdinalIgnoreCase) &&
                          variable.Name.Equals("l_PicSize", StringComparison.OrdinalIgnoreCase) &&
                          MapType(variable.Type) == "TPoint")
                            ? " = default;" : ";"));
                }
            }

            if (!function.IsProcedure)
            {
                builder.AppendLine(
                    $"    {MapType(function.ReturnType)} {context.ResultName}" +
                    (MapType(function.ReturnType) == "AnsiString" ? " = default;" : ";"));
            }

            foreach (var local in function.LocalFunctions)
            {
                var localBehavior = BehaviorAnalyzer.Analyze(behavior.FilePath, local.Name, local.BodyTokens);
                string translated = TranslateFunctionCore(
                    SemanticModelBuilder.BuildFunction(local), localBehavior, false, context);
                foreach (string line in translated.TrimEnd().Split('\n'))
                    builder.AppendLine("    " + line.TrimEnd('\r'));
                builder.AppendLine();
            }

            if (function.LocalVariables.Count > 0)
            {
                builder.AppendLine();
            }

            foreach (
                BehaviorAnalyzer.BehaviorStatement statement
                in behavior.Statements)
            {
                TranslateStatement(
                    statement,
                    function,
                    builder,
                    1);
            }

            if (!function.IsProcedure)
            {
                AppendLine(
                    builder,
                    1,
                    $"return {_currentTypeContext?.ResultName ?? "result"};");
            }

            builder.AppendLine("}");

            return builder.ToString();
        }
        finally
        {
            _currentTypeContext = previous;
        }
    }

    private string TranslateProcedureStatement(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is not null &&
            (expression.Kind == ExpressionParser.ExpressionKind.Identifier ||
             expression.Kind == ExpressionParser.ExpressionKind.MemberAccess))
        {
            var call = new ExpressionParser.ExpressionNode(
                ExpressionParser.ExpressionKind.Call,
                string.Empty,
                new[] { expression });
            return TranslateExpression(call);
        }

        return TranslateExpression(expression);
    }

    private void TranslateStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        if (statement.InheritedCall is not null)
        {
            string method = statement.InheritedCall.Method;
            string arguments = string.Join(", ",
                statement.InheritedCall.Arguments.Select(TranslateExpression));

            AppendLine(builder, indent, $"base.{method}({arguments});");
            return;
        }

        if (statement.Text.Trim().Equals(
                "inherited", StringComparison.OrdinalIgnoreCase))
        {
            string method = function.Name[
                (function.Name.LastIndexOf('.') + 1)..];
            string arguments = string.Join(", ",
                function.Parameters.Select(parameter =>
                    MapParameterKind(parameter.Kind) + parameter.Name));

            AppendLine(builder, indent, $"base.{method}({arguments});");
            return;
        }
        switch (statement.Kind)
        {
            case BehaviorAnalyzer.StatementKind.Assignment:
                TranslateAssignment(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Expression:
            case BehaviorAnalyzer.StatementKind.Call:
                AppendLine(
                    builder,
                    indent,
                    TranslateProcedureStatement(
                        statement.Expression) + ";");
                break;

            case BehaviorAnalyzer.StatementKind.If:
                TranslateIf(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Try:
                TranslateTry(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Raise:
                TranslateRaise(
                    statement,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.While:
                TranslateWhile(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.For:
                TranslateFor(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Case:
                TranslateCase(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Exit:
                AppendLine(
                    builder,
                    indent,
                    function.IsProcedure
                        ? "return;"
                        : $"return {_currentTypeContext?.ResultName ?? "result"};");
                break;

            case BehaviorAnalyzer.StatementKind.Continue:
                AppendLine(
                    builder,
                    indent,
                    "continue;");
                break;

            case BehaviorAnalyzer.StatementKind.Repeat:
                TranslateRepeat(
                    statement,
                    function,
                    builder,
                    indent);
                break;

            case BehaviorAnalyzer.StatementKind.Break:
                AppendLine(
                    builder,
                    indent,
                    "break;");
                break;

            case BehaviorAnalyzer.StatementKind.Block:
                foreach (
                    BehaviorAnalyzer.BehaviorStatement child
                    in statement.Children)
                {
                    TranslateStatement(
                        child,
                        function,
                        builder,
                        indent);
                }

                break;

            default:
                AppendLine(
                    builder,
                    indent,
                    $"// TODO: Delphi statement: {statement.Text}");
                break;
        }
    }

    private void TranslateCase(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        AppendLine(
            builder,
            indent,
            $"switch ({TranslateExpression(statement.Expression)})");

        AppendLine(
            builder,
            indent,
            "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement branch
            in statement.Children)
        {
            IReadOnlyList<string> labels =
                branch.CaseLabels ??
                Array.Empty<string>();

            foreach (string label in labels)
            {
                if (label.Equals(
                        "else",
                        StringComparison.OrdinalIgnoreCase))
                {
                    AppendLine(
                        builder,
                        indent + 1,
                        "default:");
                }
                else
                {
                    AppendLine(
                        builder,
                        indent + 1,
                        $"case {TranslateCaseLabel(label)}:");
                }
            }

            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in branch.Children)
            {
                TranslateStatement(
                    child,
                    function,
                    builder,
                    indent + 2);
            }

            AppendLine(
                builder,
                indent + 2,
                "break;");
        }

        AppendLine(
            builder,
            indent,
            "}");
    }

    private string TranslateCaseLabel(string label)
    {
        // A quoted '..' is a literal. Range separators follow a complete endpoint.
        var range = System.Text.RegularExpressions.Regex.Match(
            label, @"^('(?:''|[^'])*'|[^.]+?)\s*\.\.\s*(.+)$");
        if (range.Success)
            return $">= {TranslateCaseConstant(range.Groups[1].Value.Trim())} and <= {TranslateCaseConstant(range.Groups[2].Value.Trim())}";

        return TranslateCaseConstant(label);
    }

    private string TranslateCaseConstant(string label)
    {
        if (label.Length == 3 && label[0] == '\'' && label[2] == '\'')
            return "'" + EscapeCSharpCharacter(label[1]) + "'";
        if (label == "''''")
            return "'" + EscapeCSharpCharacter('\'') + "'";
        if (TryTranslatePascalStringLiteral(label, out string literal))
            return literal;
        if (label.StartsWith('$'))
            return "0x" + label[1..];
        if (label.StartsWith("D3DFMT_", StringComparison.OrdinalIgnoreCase))
            return TranslateIdentifier(label);
        return label;
    }

    private void TranslateIf(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        string condition =
            TranslateExpression(
                statement.Expression);

        if (condition.StartsWith("(") &&
            condition.EndsWith(")"))
        {
            condition =
                condition[1..^1];
        }

        AppendLine(
            builder,
            indent,
            $"if ({condition})");

        AppendLine(
            builder,
            indent,
            "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            TranslateStatement(
                child,
                function,
                builder,
                indent + 1);
        }

        AppendLine(
            builder,
            indent,
            "}");

        if (statement.ElseChildren is not null &&
            statement.ElseChildren.Count > 0)
        {
            AppendLine(
                builder,
                indent,
                "else");

            AppendLine(
                builder,
                indent,
                "{");

            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in statement.ElseChildren)
            {
                TranslateStatement(
                    child,
                    function,
                    builder,
                    indent + 1);
            }

            AppendLine(
                builder,
                indent,
                "}");
        }
    }

    private void TranslateRepeat(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        AppendLine(
            builder,
            indent,
            "do");

        AppendLine(
            builder,
            indent,
            "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            TranslateStatement(
                child,
                function,
                builder,
                indent + 1);
        }

        AppendLine(
            builder,
            indent,
            "}");

        string condition =
            statement.Expression is null ||
            statement.Expression.Kind ==
                ExpressionParser.ExpressionKind.Empty
                ? "false"
                : TranslateExpression(
                    statement.Expression);

        AppendLine(
            builder,
            indent,
            $"while (!({condition}));");
    }

    private void TranslateTry(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        bool hasSupportedClause =
            statement.Clauses is not null &&
            statement.Clauses.Any(clause =>
                clause.Kind.Equals(
                    "EXCEPT",
                    StringComparison.OrdinalIgnoreCase) ||
                clause.Kind.Equals(
                    "FINALLY",
                    StringComparison.OrdinalIgnoreCase));

        // C# does not allow a try block without catch/finally.
        // If the analyzer could not recover a supported clause,
        // preserve the statements as a regular scoped block rather
        // than generating syntactically invalid C#.
        AppendLine(
            builder,
            indent,
            hasSupportedClause ? "try" : "{");

        if (hasSupportedClause)
            AppendLine(builder, indent, "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            TranslateStatement(
                child,
                function,
                builder,
                indent + 1);
        }

        AppendLine(
            builder,
            indent,
            "}");

        if (!hasSupportedClause)
            return;

        foreach (
            BehaviorAnalyzer.BehaviorClause clause
            in statement.Clauses!)
        {
            if (clause.Kind.Equals(
                    "EXCEPT",
                    StringComparison.OrdinalIgnoreCase))
            {
                AppendLine(
                    builder,
                    indent,
                    "catch (Exception)");

                AppendLine(
                    builder,
                    indent,
                    "{");

                foreach (
                    BehaviorAnalyzer.BehaviorStatement handler
                    in clause.Statements)
                {
                    // Delphi "on E: TException do ..." handlers are
                    // represented as wrapper statements. Until typed
                    // exception dispatch is implemented, emit their
                    // bodies inside a compilable catch-all block.
                    IReadOnlyList<BehaviorAnalyzer.BehaviorStatement> children =
                        handler.Text.StartsWith(
                            "on ",
                            StringComparison.OrdinalIgnoreCase)
                            ? handler.Children
                            : new[] { handler };

                    foreach (
                        BehaviorAnalyzer.BehaviorStatement child
                        in children)
                    {
                        TranslateStatement(
                            child,
                            function,
                            builder,
                            indent + 1);
                    }
                }

                AppendLine(
                    builder,
                    indent,
                    "}");
            }
            else if (clause.Kind.Equals(
                         "FINALLY",
                         StringComparison.OrdinalIgnoreCase))
            {
                AppendLine(
                    builder,
                    indent,
                    "finally");

                AppendLine(
                    builder,
                    indent,
                    "{");

                foreach (
                    BehaviorAnalyzer.BehaviorStatement child
                    in clause.Statements)
                {
                    TranslateStatement(
                        child,
                        function,
                        builder,
                        indent + 1);
                }

                AppendLine(
                    builder,
                    indent,
                    "}");
            }
        }
    }

    private void TranslateWhile(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        string condition =
            TranslateExpression(
                statement.Expression);

        if (condition.StartsWith("(") &&
            condition.EndsWith(")"))
        {
            condition =
                condition[1..^1];
        }

        AppendLine(
            builder,
            indent,
            $"while ({condition})");

        AppendLine(
            builder,
            indent,
            "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            TranslateStatement(
                child,
                function,
                builder,
                indent + 1);
        }

        AppendLine(
            builder,
            indent,
            "}");
    }

    private void TranslateFor(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        if (statement.ForLoop is null)
        {
            AppendLine(
                builder,
                indent,
                $"// TODO: Delphi for: {statement.Text}");

            return;
        }

        BehaviorAnalyzer.ForLoopInfo loop =
            statement.ForLoop;

        string from =
            TranslateExpression(loop.From);

        string to =
            TranslateExpression(loop.To);

        string variable =
            loop.Variable;

        string comparison =
            loop.Direction.Equals(
                "down",
                StringComparison.OrdinalIgnoreCase)
                ? ">="
                : "<=";

        string increment =
            loop.Direction.Equals(
                "down",
                StringComparison.OrdinalIgnoreCase)
                ? "--"
                : "++";

        AppendLine(
            builder,
            indent,
            $"for ({variable} = {from}; " +
            $"{variable} {comparison} {to}; " +
            $"{variable}{increment})");

        AppendLine(
            builder,
            indent,
            "{");

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            TranslateStatement(
                child,
                function,
                builder,
                indent + 1);
        }

        AppendLine(
            builder,
            indent,
            "}");
    }

    private string TranslateIdentifier(
        string text)
    {
        if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
            _resolver is not null &&
            _currentTypeContext?.ResolveIdentifier(text) is null)
        {
            string? constant = text.ToUpperInvariant() switch
            {
                "D3DADAPTER_DEFAULT" => "D3DADAPTER_DEFAULT",
                "D3DENUM_NO_WHQL_LEVEL" => "D3DENUM_NO_WHQL_LEVEL",
                "D3DFMT_UNKNOWN" => "D3DFMT_UNKNOWN",
                "D3DFMT_A8R8G8B8" => "D3DFMT_A8R8G8B8",
                "D3DFMT_X8R8G8B8" => "D3DFMT_X8R8G8B8",
                "D3DFMT_R5G6B5" => "D3DFMT_R5G6B5",
                "D3DFMT_X1R5G5B5" => "D3DFMT_X1R5G5B5",
                "D3DFMT_A1R5G5B5" => "D3DFMT_A1R5G5B5",
                "D3DFMT_INDEX16" => "D3DFMT_INDEX16",
                "D3DFMT_D16" => "D3DFMT_D16",
                "D3DMULTISAMPLE_NONE" => "D3DMULTISAMPLE_NONE",
                "D3DDEVTYPE_HAL" => "D3DDEVTYPE_HAL",
                "D3DCREATE_FPU_PRESERVE" => "D3DCREATE_FPU_PRESERVE",
                "D3DCREATE_SOFTWARE_VERTEXPROCESSING" => "D3DCREATE_SOFTWARE_VERTEXPROCESSING",
                "D3DCREATE_HARDWARE_VERTEXPROCESSING" => "D3DCREATE_HARDWARE_VERTEXPROCESSING",
                _ => null
            };
            if (constant is not null && !_resolver.HasVisibleValueDeclaration(text, _unitName!))
                return "global::FireURQ.Runtime.D3D8." + constant;
        }

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "true";

        if (text.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "false";

        if (text.Equals(
            "Result",
            StringComparison.OrdinalIgnoreCase))
        {
            return _currentTypeContext?.ResultName ?? "result";
        }

        if (text.Equals(
            "nil",
            StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        return _currentTypeContext?.ResolveSpelling(text) ?? text;
    }

    private void TranslateAssignment(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        if (statement.Assignment is null)
        {
            AppendLine(
                builder,
                indent,
                $"// TODO: Delphi assignment: {statement.Text}");

            return;
        }

        ExpressionParser.ExpressionNode targetExpression =
            statement.Assignment.Target;

        // Delphi dynamic API loaders sometimes express assignment to a
        // procedural variable through an address-of target. In this one
        // narrow case, GetProcAddress returns the value to store in the
        // procedural variable; emitting "@target" as a C# lvalue is invalid.
        if (IsAddressOfGetProcAddressTarget(
                targetExpression,
                statement.Assignment.Value))
        {
            targetExpression = targetExpression.Children[0];
        }

        if (_resolver is not null &&
            targetExpression.Kind == ExpressionParser.ExpressionKind.Dereference &&
            targetExpression.Children.Count == 1)
        {
            var pointer = targetExpression.Children[0];
            string? pointerType = ResolveExpressionType(pointer);
            if (pointerType is not null &&
                _resolver.Resolve(pointerType, _unitName) is
                {
                    IsResolved: true,
                    PointerDepth: 1,
                    DeclaringUnitName: null
                } resolution &&
                string.Equals(resolution.ResolvedName, "TPoint",
                    StringComparison.OrdinalIgnoreCase) &&
                _resolver.Resolve("TPoint", _unitName).Chain.Count == 0)
            {
                var point = statement.Assignment.Value;
                string translatedPoint;
                if (point.Kind == ExpressionParser.ExpressionKind.Call &&
                    point.Children.Count == 3 &&
                    point.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
                    string.Equals(point.Children[0].Text, "Point",
                        StringComparison.OrdinalIgnoreCase) &&
                    _unitName is not null &&
                    ResolveExpressionType(point.Children[0]) is null &&
                    !_resolver.HasVisibleValueDeclaration("Point", _unitName))
                {
                    string x = TranslateExpression(point.Children[1]);
                    string y = TranslateExpression(point.Children[2]);
                    translatedPoint =
                        $"global::FireURQ.Runtime.DelphiGeometry.Point({x}, {y})";
                }
                else
                {
                    translatedPoint = TranslateExpression(point);
                }

                AppendLine(builder, indent,
                    $"global::System.Runtime.InteropServices.Marshal.StructureToPtr<global::FireURQ.Runtime.TPoint>({translatedPoint}, {TranslateExpression(pointer)}, false);");
                return;
            }
        }

        string target =
            TranslateExpression(targetExpression);

        string value =
            IsNilLiteral(statement.Assignment.Value) &&
            IsIntPtrBackedExpression(targetExpression)
                ? "IntPtr.Zero"
                : TranslateExpression(statement.Assignment.Value);

        if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_currentTypeContext?.ResolveIdentifier("l_Index"), "PWord",
                StringComparison.OrdinalIgnoreCase))
        {
            if (targetExpression.Kind == ExpressionParser.ExpressionKind.Dereference &&
                targetExpression.Children.Count == 1 &&
                targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
                targetExpression.Children[0].Text.Equals("l_Index", StringComparison.OrdinalIgnoreCase))
            {
                AppendLine(builder, indent,
                    $"global::System.Runtime.InteropServices.Marshal.WriteInt16(l_Index, unchecked((short)({value})));");
                return;
            }

            if (target.Equals("(uint)(l_Index)", StringComparison.OrdinalIgnoreCase) &&
                value.Equals("((uint)(l_Index) + 2)", StringComparison.OrdinalIgnoreCase))
            {
                AppendLine(builder, indent, "l_Index = global::System.IntPtr.Add(l_Index, 2);");
                return;
            }
        }

        // D3DX reports unsigned dimensions; Delphi TPoint stores signed coordinates.
        if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
            function.Name.Equals("Texture_Load", StringComparison.OrdinalIgnoreCase) &&
            ((target.Equals("l_PicSize.X", StringComparison.OrdinalIgnoreCase) &&
              value.Equals("l_ImgInfo.Width", StringComparison.OrdinalIgnoreCase)) ||
             (target.Equals("l_PicSize.Y", StringComparison.OrdinalIgnoreCase) &&
              value.Equals("l_ImgInfo.Height", StringComparison.OrdinalIgnoreCase))))
        {
            value = $"unchecked((int)({value}))";
        }

        if (!function.IsProcedure &&
            target.Equals(
                "Result",
                StringComparison.OrdinalIgnoreCase))
        {
            target = _currentTypeContext?.ResultName ?? "result";
        }

        AppendLine(
            builder,
            indent,
            $"{target} = {value};");
    }

    private static bool IsAddressOfGetProcAddressTarget(
        ExpressionParser.ExpressionNode target,
        ExpressionParser.ExpressionNode value)
    {
        return target.Kind == ExpressionParser.ExpressionKind.AddressOf &&
               target.Children.Count == 1 &&
               target.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
               value.Kind == ExpressionParser.ExpressionKind.Call &&
               value.Children.Count > 0 &&
               value.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
               value.Children[0].Text.Equals(
                   "GetProcAddress",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void TranslateRaise(
        BehaviorAnalyzer.BehaviorStatement statement,
        StringBuilder builder,
        int indent)
    {
        ExpressionParser.ExpressionNode? expression =
            statement.Expression;

        if (expression is null)
        {
            AppendLine(
                builder,
                indent,
                "throw;");

            return;
        }

        if (expression.Kind ==
                ExpressionParser.ExpressionKind.Call &&
            expression.Children.Count >= 1)
        {
            ExpressionParser.ExpressionNode target =
                expression.Children[0];

            if (target.Kind ==
                    ExpressionParser.ExpressionKind.MemberAccess &&
                target.Children.Count >= 2 &&
                string.Equals(
                    target.Children[1].Text,
                    "Create",
                    StringComparison.OrdinalIgnoreCase))
            {
                string exceptionType =
                    TranslateExpression(
                        target.Children[0]);

                var arguments =
                    new List<string>();

                for (int i = 1;
                     i < expression.Children.Count;
                     i++)
                {
                    arguments.Add(
                        TranslateExpression(
                            expression.Children[i]));
                }

                AppendLine(
                    builder,
                    indent,
                    $"throw new {exceptionType}({string.Join(", ", arguments)});");

                return;
            }
        }

        AppendLine(
            builder,
            indent,
            $"throw {TranslateExpression(expression)};");
    }

    private bool TryGetStandardListFactoryTarget(
        ExpressionParser.ExpressionNode expression,
        out string target)
    {
        target = string.Empty;
        if (_resolver is null ||
            expression.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
            expression.Children.Count != 2)
            return false;

        var receiver = expression.Children[0];
        var member = expression.Children[1];
        if (receiver.Kind != ExpressionParser.ExpressionKind.Identifier ||
            member.Kind != ExpressionParser.ExpressionKind.Identifier ||
            !member.Text.Equals("Create", StringComparison.OrdinalIgnoreCase) ||
            ResolveExpressionType(receiver) is not null)
            return false;

        string typeName;
        if (receiver.Text.Equals("TList", StringComparison.OrdinalIgnoreCase))
            typeName = "TList";
        else if (receiver.Text.Equals("TObjectList", StringComparison.OrdinalIgnoreCase))
            typeName = "TObjectList";
        else if (receiver.Text.Equals("TInterfaceList", StringComparison.OrdinalIgnoreCase))
            typeName = "TInterfaceList";
        else if (receiver.Text.Equals("TStringList", StringComparison.OrdinalIgnoreCase))
            typeName = "TStringList";
        else
            return false;

        TypeResolution resolution = _resolver.Resolve(typeName, _unitName);
        if (!resolution.IsResolved || resolution.Chain.Count > 0)
            return false;

        target = typeName + ".Create";
        return true;
    }

    private string TranslateCallTarget(
        ExpressionParser.ExpressionNode expression)
    {
        if (TryGetStandardListFactoryTarget(expression, out string target))
            return target;

        return expression.Kind switch
        {
            ExpressionParser.ExpressionKind.MemberAccess => TranslateMemberAccess(expression),
            ExpressionParser.ExpressionKind.Identifier => TranslateIdentifier(expression.Text),
            _ => TranslateExpression(expression)
        };
    }

    private bool IsF2LinkParserBareCall(ExpressionParser.ExpressionNode expression)
    {
        if (!string.Equals(_unitName, "f2LinkParser", StringComparison.OrdinalIgnoreCase))
            return false;

        if (expression.Kind == ExpressionParser.ExpressionKind.Identifier)
        {
            return expression.Text.Equals("GetLinkRegEx", StringComparison.OrdinalIgnoreCase) &&
                _currentTypeContext?.ResolveIdentifier(expression.Text) is null;
        }

        if (expression.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
            expression.Children.Count != 2)
            return false;

        var receiver = expression.Children[0];
        string member = expression.Children[1].Text;
        return (receiver.Text.Equals("TRegExpr", StringComparison.OrdinalIgnoreCase) &&
                member.Equals("Create", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(ResolveExpressionType(receiver), "TRegExpr", StringComparison.OrdinalIgnoreCase) &&
                member.Equals("ExecNext", StringComparison.OrdinalIgnoreCase));
    }
    private string TranslateExpression(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null)
            return string.Empty;

        if (TryGetStandardListFactoryTarget(expression, out string factoryTarget))
            return factoryTarget + "()";

        if (IsF2LinkParserBareCall(expression))
            return TranslateCallTarget(expression) + "()";

        return expression.Kind switch
        {
            ExpressionParser.ExpressionKind.Empty =>
                string.Empty,

            ExpressionParser.ExpressionKind.Identifier =>
                TranslateIdentifier(expression.Text),

            ExpressionParser.ExpressionKind.Literal =>
                TranslateLiteral(expression.Text),

            ExpressionParser.ExpressionKind.Binary =>
                TranslateBinary(expression),

            ExpressionParser.ExpressionKind.Unary =>
                TranslateUnary(expression),

            ExpressionParser.ExpressionKind.MemberAccess =>
                TranslateMemberAccess(expression),

            ExpressionParser.ExpressionKind.IndexAccess =>
                TranslateIndexAccess(expression),

            ExpressionParser.ExpressionKind.Call =>
                TranslateCall(expression),

            ExpressionParser.ExpressionKind.TypeTest =>
                TranslateTypeTest(expression),

            ExpressionParser.ExpressionKind.TypeCast =>
                TranslateTypeCast(expression),

            ExpressionParser.ExpressionKind.Set =>
                TranslateSet(expression),

            ExpressionParser.ExpressionKind.AddressOf =>
                TranslateAddressOf(expression),

            ExpressionParser.ExpressionKind.Dereference =>
                expression.Children.Count == 0
                    ? expression.Text
                    : TranslateExpression(expression.Children[0]),

            _ =>
                expression.Text
        };
    }

    private string TranslateBinary(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        // Delphi nil comparisons must respect the translated pointer representation:
        // pointer aliases are IntPtr-backed, while procedure/delegate references use null.
        if (TryTranslateNilComparison(expression, out string nilComparison))
            return nilComparison;

        string left =
            TranslateExpression(
                expression.Children[0]);

        string right =
            TranslateExpression(
                expression.Children[1]);

        if (string.Equals(
                expression.Text,
                "in",
                StringComparison.OrdinalIgnoreCase))
        {
            return TranslateSetMembership(expression);
        }

        string? leftType =
            ResolveExpressionType(
                expression.Children[0]);

        string? rightType =
            ResolveExpressionType(
                expression.Children[1]);

        bool isBoolean =
            IsBooleanType(leftType) || IsBooleanType(rightType);

        string op =
            expression.Text.ToLowerInvariant() switch
            {
                "=" => "==",
                "<>" => "!=",
                "and" => isBoolean ? "&&" : "&",
                "or" => isBoolean ? "||" : "|",
                "div" => "/",
                "mod" => "%",
                "shl" => "<<",
                "shr" => ">>",
                "xor" => "^",
                _ => expression.Text
            };

        return $"({left} {op} {right})";
    }

    private bool TryTranslateNilComparison(
        ExpressionParser.ExpressionNode expression,
        out string translated)
    {
        translated = string.Empty;

        string op = expression.Text.Trim();
        if (op != "=" && op != "<>")
            return false;

        ExpressionParser.ExpressionNode left = expression.Children[0];
        ExpressionParser.ExpressionNode right = expression.Children[1];

        ExpressionParser.ExpressionNode? valueExpression = null;

        if (IsNilLiteral(right))
            valueExpression = UnwrapAddressOfIdentifier(left);
        else if (IsNilLiteral(left))
            valueExpression = UnwrapAddressOfIdentifier(right);

        if (valueExpression is null)
            return false;

        string nullValue = IsIntPtrBackedExpression(valueExpression)
            ? "IntPtr.Zero"
            : "null";

        string value = TranslateExpression(valueExpression);
        translated = $"({value} {(op == "=" ? "==" : "!=")} {nullValue})";
        return true;
    }

    private static ExpressionParser.ExpressionNode UnwrapAddressOfIdentifier(
        ExpressionParser.ExpressionNode expression)
    {
        return IsAddressOfIdentifier(expression)
            ? expression.Children[0]
            : expression;
    }

    private bool IsIntPtrBackedExpression(
        ExpressionParser.ExpressionNode expression)
    {
        string? typeName = ResolveExpressionType(expression);

        // Resolve the native D2D vertex-buffer field when no local shadows it.
        if (typeName is null &&
            string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
            expression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            expression.Text.Equals("f_VertArray", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        if (_resolver is not null)
            return MapType(typeName) == "IntPtr";

        // Delphi pointer aliases conventionally begin with P (PByteArray,
        // PMD5Context, etc.). Their declarations are emitted as IntPtr aliases.
        return typeName.Length > 1 &&
               typeName.StartsWith("P", StringComparison.OrdinalIgnoreCase) &&
               char.IsUpper(typeName[1]);
    }

    private static bool IsAddressOfIdentifier(
        ExpressionParser.ExpressionNode expression)
    {
        return expression.Kind == ExpressionParser.ExpressionKind.AddressOf &&
               expression.Children.Count == 1 &&
               expression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier;
    }

    private static bool IsNilLiteral(
        ExpressionParser.ExpressionNode expression)
    {
        // ExpressionParser classifies Pascal keywords as Identifier nodes,
        // so "nil" commonly arrives here as Identifier rather than Literal.
        return (expression.Kind == ExpressionParser.ExpressionKind.Literal ||
                expression.Kind == ExpressionParser.ExpressionKind.Identifier) &&
               expression.Text.Equals(
                   "nil",
                   StringComparison.OrdinalIgnoreCase);
    }

    private string TranslateUnary(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count == 0)
            return expression.Text;

        string operand =
            TranslateExpression(
                expression.Children[0]);

        string? operandType =
            ResolveExpressionType(
                expression.Children[0]);

        bool isBoolean = IsBooleanType(operandType);

        string op =
            expression.Text.ToLowerInvariant() switch
            {
                "not" => isBoolean ? "!" : "~",
                "+" => "+",
                "-" => "-",
                _ => expression.Text
            };

        return $"{op}{operand}";
    }

    private string? ResolveExpressionType(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null ||
            _currentTypeContext is null)
        {
            return null;
        }

        if (expression.Kind ==
            ExpressionParser.ExpressionKind.Identifier)
        {
            return _currentTypeContext.ResolveIdentifier(
                expression.Text);
        }

        return null;
    }

    private string TranslateMemberAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        string target =
            TranslateExpression(
                expression.Children[0]);

        string member =
            TranslateExpression(
                expression.Children[1]);

        return $"{target}.{member}";
    }

    private string TranslateIndexAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        string target =
            TranslateExpression(
                expression.Children[0]);

        string index =
            TranslateExpression(
                expression.Children[1]);

        return $"{target}[{index}]";
    }

private bool TryTranslateStreamCall(
    ExpressionParser.ExpressionNode expression,
    ExpressionParser.ExpressionNode target,
    out string translated)
{
    translated = string.Empty;
    if (_resolver is null ||
        expression.Children.Count != 3 ||
        target.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
        target.Children.Count != 2 ||
        target.Children[1].Kind != ExpressionParser.ExpressionKind.Identifier)
        return false;

    string operation = target.Children[1].Text.ToLowerInvariant() switch
    {
        "read" => "Read",
        "readbuffer" => "ReadBuffer",
        "write" => "Write",
        "writebuffer" => "WriteBuffer",
        _ => string.Empty
    };
    if (operation.Length == 0)
        return false;

    string? receiverType = ResolveExpressionType(target.Children[0]);
    if (receiverType is null ||
        !_resolver.IsInheritedTStreamOperation(
            receiverType, operation, _unitName))
        return false;

    ExpressionParser.ExpressionNode buffer = expression.Children[1];
    string argument;
    if (buffer.Kind == ExpressionParser.ExpressionKind.Dereference &&
        buffer.Children.Count == 1)
    {
        string? pointerType = ResolveExpressionType(buffer.Children[0]);
        if (pointerType is null ||
            !_resolver.Resolve(pointerType, _unitName).IsPointer)
            return false;
        argument = TranslateExpression(buffer.Children[0]);
    }
    else
    {
        string? bufferType = ResolveExpressionType(buffer);
        if (bufferType is null)
            return false;
        string mapped = MapType(bufferType);
        bool scalar = mapped is
            "bool" or "byte" or "sbyte" or "char" or
            "short" or "ushort" or "int" or "uint" or
            "long" or "ulong" or "float" or "double" or
            "IntPtr" or "UIntPtr";

        if (!scalar && mapped != "byte[]")
            return false;
        if (scalar && buffer.Kind != ExpressionParser.ExpressionKind.Identifier)
            return false;

        argument = TranslateExpression(buffer);
        if (scalar && operation is "Read" or "ReadBuffer")
            argument = "ref " + argument;
    }

    string receiver = TranslateExpression(target.Children[0]);
    string count = TranslateExpression(expression.Children[2]);
    translated = $"{receiver}.{operation}({argument}, checked((int)({count})))";
    return true;
}

    private bool IsStandardAnsiStringValue(
        ExpressionParser.ExpressionNode expression)
    {
        string? type = ResolveExpressionType(expression);
        if (_resolver is null || type is null)
            return false;

        TypeResolution resolution = _resolver.Resolve(type, _unitName);
        return resolution.IsResolved &&
            resolution.DeclaringUnitName is null &&
            !resolution.IsPointer &&
            MapType(type) == "AnsiString";
    }

    private bool TryTranslateAnsiStringCall(
        ExpressionParser.ExpressionNode expression,
        out string translated)
    {
        translated = string.Empty;
        if (_resolver is null || _unitName is null ||
            expression.Kind != ExpressionParser.ExpressionKind.Call ||
            expression.Children.Count < 2)
            return false;

        var target = expression.Children[0];

        if (target.Kind == ExpressionParser.ExpressionKind.Identifier &&
            !_resolver.HasVisibleValueDeclaration(target.Text, _unitName) &&
            ResolveExpressionType(target) is null)
        {
            var value = expression.Children[1];
            if (value.Kind == ExpressionParser.ExpressionKind.Identifier &&
                IsStandardAnsiStringValue(value))
            {
                string name = TranslateExpression(value);
                if (target.Text.Equals("SetLength", StringComparison.OrdinalIgnoreCase) &&
                    expression.Children.Count == 3)
                {
                    string length = TranslateExpression(expression.Children[2]);
                    translated = $"{name} = {name}.WithLength(checked((int)({length})))";
                    return true;
                }

                if (target.Text.Equals("Length", StringComparison.OrdinalIgnoreCase) &&
                    expression.Children.Count == 2)
                {
                    translated = $"{name}.Length";
                    return true;
                }
            }
        }

        if (target.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
            target.Children.Count != 2 || expression.Children.Count != 3)
            return false;

        var receiver = target.Children[0];
        var member = target.Children[1];
        string? receiverType = ResolveExpressionType(receiver);
        if (receiverType is null ||
            member.Kind != ExpressionParser.ExpressionKind.Identifier)
            return false;

        string operation = member.Text.ToLowerInvariant() switch
        {
            "read" => "Read",
            "readbuffer" => "ReadBuffer",
            "write" => "Write",
            "writebuffer" => "WriteBuffer",
            _ => string.Empty
        };
        if (operation.Length == 0 ||
            !_resolver.IsInheritedTStreamOperation(
                receiverType, operation, _unitName))
            return false;

        var buffer = expression.Children[1];
        if (buffer.Kind != ExpressionParser.ExpressionKind.Dereference ||
            buffer.Children.Count != 1)
            return false;

        var cast = buffer.Children[0];
        if (cast.Kind != ExpressionParser.ExpressionKind.Call ||
            cast.Children.Count != 2 ||
            cast.Children[0].Kind != ExpressionParser.ExpressionKind.Identifier)
            return false;

        string castName = cast.Children[0].Text;
        if (!castName.Equals("PChar", StringComparison.OrdinalIgnoreCase) &&
            !castName.Equals("PAnsiChar", StringComparison.OrdinalIgnoreCase))
            return false;
        if (_resolver.HasVisibleValueDeclaration(castName, _unitName))
            return false;
        TypeResolution pointerResolution = _resolver.Resolve(castName, _unitName);
        if (!pointerResolution.IsResolved || pointerResolution.Chain.Count > 0)
            return false;

        var valueExpression = cast.Children[1];
        if (valueExpression.Kind != ExpressionParser.ExpressionKind.Identifier ||
            !IsStandardAnsiStringValue(valueExpression))
            return false;

        string argument = TranslateExpression(valueExpression);
        if (operation is "Read" or "ReadBuffer")
            argument = "ref " + argument;

        string count = TranslateExpression(expression.Children[2]);
        translated = "global::FireURQ.Runtime.AnsiStringStreams." +
            $"{operation}({TranslateExpression(receiver)}, {argument}, " +
            $"checked((int)({count})))";
        return true;
    }
    private bool TryTranslateDeclaredTypeCast(
        ExpressionParser.ExpressionNode expression,
        out string translated)
    {
        translated = string.Empty;
        if (_resolver is null || _unitName is null ||
            expression.Kind != ExpressionParser.ExpressionKind.Call ||
            expression.Children.Count != 2)
            return false;

        var target = expression.Children[0];
        if (target.Kind != ExpressionParser.ExpressionKind.Identifier ||
            ResolveExpressionType(target) is not null ||
            _resolver.HasVisibleNonTypeDeclaration(target.Text, _unitName))
            return false;

        TypeResolution resolution = _resolver.Resolve(target.Text, _unitName);
        if (!resolution.IsResolved || resolution.IsPointer ||
            (resolution.Chain.Count == 0 &&
             resolution.DeclaringUnitName is null))
            return false;

        string type = MapType(target.Text);
        string value = TranslateExpression(expression.Children[1]);
        translated = $"(({type})({value}))";
        return true;
    }
private string TranslateCall(
    ExpressionParser.ExpressionNode expression)
{
    if (TryTranslateDeclaredTypeCast(expression, out string declaredCast))
        return declaredCast;

    if (TryTranslateAnsiStringCall(expression, out string ansiStringCall))
        return ansiStringCall;

    if (expression.Children.Count == 0)
        return expression.Text;

    ExpressionParser.ExpressionNode targetExpression =
        expression.Children[0];

    if (string.Equals(_unitName, "f2PicturePool", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 6 &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[0].Text.Equals("gD2DE", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Children[1].Text.Equals("Texture_Load", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[5] is { Kind: ExpressionParser.ExpressionKind.AddressOf } address &&
        address.Children.Count == 1 &&
        address.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } point &&
        point.Text.Equals("l_PicSize", StringComparison.OrdinalIgnoreCase) &&
        MapType(ResolveExpressionType(point)) == "TPoint")
    {
        string call = TranslateCallTarget(targetExpression);
        string textureArguments = string.Join(", ", expression.Children.Skip(1)
            .Take(4).Select(TranslateExpression));
        return "global::FireURQ.Runtime.DelphiGeometry.WithPointAddress(" +
            $"ref {TranslateExpression(point)}, __fireurqPointAddress => " +
            $"{call}({textureArguments}, __fireurqPointAddress))";
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("D3DXGetImageInfoFromFileInMemory",
            StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 4 &&
        expression.Children[3].Kind == ExpressionParser.ExpressionKind.Identifier &&
        string.Equals(ResolveExpressionType(expression.Children[3]), "TD3DXImageInfo",
            StringComparison.OrdinalIgnoreCase))
    {
        string data = TranslateExpression(expression.Children[1]);
        string size = TranslateExpression(expression.Children[2]);
        string info = TranslateExpression(expression.Children[3]);

        return "global::FireURQ.Runtime.D3DX8.D3DXGetImageInfoFromFileInMemory" +
            $"({data}, {size}, out {info})";
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("Format", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 3 &&
        expression.Children[1].Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children[1].Text.Equals("aFormat", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[2].Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children[2].Text.Equals("Args", StringComparison.OrdinalIgnoreCase) &&
        MapType(ResolveExpressionType(expression.Children[1])) == "string" &&
        MapType(ResolveExpressionType(expression.Children[2])) == "TVarRec[]")
    {
        string format = TranslateExpression(expression.Children[1]);
        string args = TranslateExpression(expression.Children[2]);
        return $"global::FireURQ.Runtime.D2dLogFormat.Format({format}, {args})";
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("BASS_GetDeviceInfo", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 3 &&
        expression.Children[2].Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children[2].Text.Equals("l_DevInfo", StringComparison.OrdinalIgnoreCase) &&
        MapType(ResolveExpressionType(expression.Children[2])) == "BASS_DEVICEINFO")
    {
        string device = TranslateExpression(expression.Children[1]);
        string info = TranslateExpression(expression.Children[2]);
        return $"BASS_GetDeviceInfo({device}, ref {info})";
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 4)
    {
        var destination = expression.Children[3];
        if (targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
            targetExpression.Children.Count == 2 &&
            targetExpression.Children[0].Text.Equals("f_D3D", StringComparison.OrdinalIgnoreCase) &&
            targetExpression.Children[1].Text.Equals("GetAdapterIdentifier", StringComparison.OrdinalIgnoreCase) &&
            destination.Kind == ExpressionParser.ExpressionKind.Identifier &&
            MapType(ResolveExpressionType(destination)) == "D3DADAPTER_IDENTIFIER8")
        {
            string adapter = TranslateExpression(expression.Children[1]);
            string flags = TranslateExpression(expression.Children[2]);
            return $"f_D3D.GetAdapterIdentifier({adapter}, {flags}, out {TranslateExpression(destination)})";
        }

        // Preserve the two one-based reads of the copied driver version.
        if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            targetExpression.Text.Equals("Move", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TranslateExpression(expression.Children[1]),
                "l_AdID.DriverVersion", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TranslateExpression(expression.Children[2]),
                "l_DVer", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TranslateExpression(destination),
                "SizeOf(Cardinal)", StringComparison.OrdinalIgnoreCase) &&
            MapType(ResolveExpressionType(expression.Children[2])) == "ushort[]")
        {
            return "l_DVer = new ushort[] { 0, " +
                "(ushort)l_AdID.DriverVersionSecondWord, " +
                "(ushort)l_AdID.DriverVersionFirstWord }";
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase))
    {
        if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            targetExpression.Text.Equals("Direct3DCreate8", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 2)
        {
            return "global::FireURQ.Runtime.D3D8.Create(" +
                TranslateExpression(expression.Children[1]) + ")";
        }

        if (targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
            targetExpression.Children.Count == 2 &&
            targetExpression.Children[0].Text.Equals("f_D3D", StringComparison.OrdinalIgnoreCase) &&
            targetExpression.Children[1].Text.Equals("GetAdapterDisplayMode", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 3 &&
            expression.Children[2].Kind == ExpressionParser.ExpressionKind.Identifier &&
            MapType(ResolveExpressionType(expression.Children[2])) == "D3DDISPLAYMODE")
        {
            string adapter = TranslateExpression(expression.Children[1]);
            string mode = TranslateExpression(expression.Children[2]);
            return $"f_D3D.GetAdapterDisplayMode({adapter}, out {mode})";
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        _resolver is not null &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("Failed", StringComparison.OrdinalIgnoreCase) &&
        ResolveExpressionType(targetExpression) is null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName!) &&
        expression.Children.Count == 2)
    {
        var argument = expression.Children[1];
        if (argument.Kind == ExpressionParser.ExpressionKind.Call &&
            argument.Children.Count == 3 &&
            argument.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } method &&
            method.Children.Count == 2 &&
            method.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            method.Children[0].Text.Equals("f_D3D", StringComparison.OrdinalIgnoreCase) &&
            method.Children[1].Text.Equals("GetAdapterDisplayMode", StringComparison.OrdinalIgnoreCase) &&
            argument.Children[2].Kind == ExpressionParser.ExpressionKind.Identifier &&
            MapType(ResolveExpressionType(argument.Children[2])) == "D3DDISPLAYMODE")
        {
            return $"({TranslateExpression(argument)} < 0)";
        }
    }

    // Preserve both CreateDevice attempts in Td2dCore.GfxInit.
    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("Failed", StringComparison.OrdinalIgnoreCase) &&
        _resolver is not null &&
        ResolveExpressionType(targetExpression) is null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName!) &&
        expression.Children.Count == 2 &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Call } create &&
        create.Children.Count == 7 &&
        create.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } createTarget &&
        createTarget.Children.Count == 2 &&
        createTarget.Children[0].Text.Equals("f_D3D", StringComparison.OrdinalIgnoreCase) &&
        createTarget.Children[1].Text.Equals("CreateDevice", StringComparison.OrdinalIgnoreCase) &&
        create.Children[5].Text.Equals("f_D3DPP", StringComparison.OrdinalIgnoreCase) &&
        create.Children[6].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase))
    {
        var createArgs = create.Children.Skip(1).Select(TranslateExpression).ToArray();
        createArgs[4] = "ref " + createArgs[4];
        createArgs[5] = "out " + createArgs[5];
        return $"(f_D3D.CreateDevice({string.Join(", ", createArgs)}) < 0)";
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[0].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Children[1].Kind == ExpressionParser.ExpressionKind.Identifier)
    {
        string method = targetExpression.Children[1].Text;
        if (method.Equals("Reset", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 2 &&
            expression.Children[1].Kind == ExpressionParser.ExpressionKind.Identifier &&
            expression.Children[1].Text.Equals("f_D3DPP", StringComparison.OrdinalIgnoreCase))
        {
            return $"f_D3DDevice.Reset(ref {TranslateExpression(expression.Children[1])})";
        }

        if (method.Equals("Present", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 5)
        {
            var presentArgs = expression.Children.Skip(1).Select((argument, index) => (IsNilLiteral(argument) || (index == 2 && TranslateExpression(argument) == "0")) ? "global::System.IntPtr.Zero" : TranslateExpression(argument));
            return $"f_D3DDevice.Present({string.Join(", ", presentArgs)})";
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase))
    {
        ExpressionParser.ExpressionNode surfaceCall = expression;
        bool checkHresult =
            targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            targetExpression.Text.Equals("Failed", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 2 &&
            expression.Children[1].Kind == ExpressionParser.ExpressionKind.Call &&
            ResolveExpressionType(targetExpression) is null &&
            _resolver is not null &&
            !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName!);

        if (checkHresult)
            surfaceCall = expression.Children[1];

        if (surfaceCall.Children.Count > 0 &&
            surfaceCall.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } surfaceTarget &&
            surfaceTarget.Children.Count == 2 &&
            surfaceTarget.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            surfaceTarget.Children[0].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase))
        {
            string? surfaceMethod = surfaceTarget.Children[1].Text.ToUpperInvariant() switch
            {
                "GETRENDERTARGET" when surfaceCall.Children.Count == 2 => "GetRenderTarget",
                "GETDEPTHSTENCILSURFACE" when surfaceCall.Children.Count == 2 => "GetDepthStencilSurface",
                "CREATEDEPTHSTENCILSURFACE" when surfaceCall.Children.Count == 6 => "CreateDepthStencilSurface",
                "SETRENDERTARGET" when surfaceCall.Children.Count == 3 => "SetRenderTarget",
                _ => null
            };

            if (surfaceMethod is not null)
            {
                var surfaceArgs = surfaceCall.Children.Skip(1)
                    .Select(TranslateExpression).ToArray();

                if (surfaceMethod == "CreateDepthStencilSurface")
                {
                    surfaceArgs[0] = $"unchecked((uint)({surfaceArgs[0]}))";
                    surfaceArgs[1] = $"unchecked((uint)({surfaceArgs[1]}))";
                }

                if (surfaceMethod != "SetRenderTarget")
                    surfaceArgs[^1] = "out " + surfaceArgs[^1];

                string translatedSurfaceCall =
                    $"f_D3DDevice.{surfaceMethod}({string.Join(", ", surfaceArgs)})";
                return checkHresult ? $"({translatedSurfaceCall} < 0)" : translatedSurfaceCall;
            }
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 3 &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[1].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[1].Text.Equals("GetSurfaceLevel", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[2].Kind == ExpressionParser.ExpressionKind.Identifier &&
        MapType(ResolveExpressionType(expression.Children[2])) == "IDirect3DSurface8")
    {
        var textureTarget = targetExpression.Children[0];
        if (textureTarget.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
            textureTarget.Children.Count == 2 &&
            textureTarget.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            textureTarget.Children[0].Text.Equals("aTarget", StringComparison.OrdinalIgnoreCase) &&
            textureTarget.Children[1].Text.Equals("Tex", StringComparison.OrdinalIgnoreCase))
        {
            string receiver = TranslateExpression(textureTarget);
            string level = TranslateExpression(expression.Children[1]);
            string surface = TranslateExpression(expression.Children[2]);
            return $"{receiver}.GetSurfaceLevel({level}, out {surface})";
        }
    }

    // Bind the two buffer allocations in Td2dCore.
    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        _resolver is not null &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("Failed", StringComparison.OrdinalIgnoreCase) &&
        ResolveExpressionType(targetExpression) is null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName!) &&
        expression.Children.Count == 2 &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Call } bufferCall &&
        bufferCall.Children.Count == 6 &&
        bufferCall.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } bufferTarget &&
        bufferTarget.Children.Count == 2 &&
        bufferTarget.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        bufferTarget.Children[0].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase) &&
        bufferCall.Children[2].Text.Equals("D3DUSAGE_WRITEONLY", StringComparison.OrdinalIgnoreCase) &&
        bufferCall.Children[4].Text.Equals("D3DPOOL_DEFAULT", StringComparison.OrdinalIgnoreCase) &&
        bufferCall.Children[5].Kind == ExpressionParser.ExpressionKind.Identifier)
    {
        string method = bufferTarget.Children[1].Text;
        string destination = bufferCall.Children[5].Text;
        string length = TranslateExpression(bufferCall.Children[1]);

        if (method.Equals("CreateVertexBuffer", StringComparison.OrdinalIgnoreCase) &&
            destination.Equals("f_VB", StringComparison.OrdinalIgnoreCase) &&
            bufferCall.Children[3].Text.Equals("D3DFVF_D2DVERTEX", StringComparison.OrdinalIgnoreCase) &&
            length == "(VertexBufferSize * SizeOf(Td2dVertex))")
        {
            return "(f_D3DDevice.CreateVertexBuffer(" +
                "checked((uint)(VertexBufferSize * global::System.Runtime.InteropServices.Marshal.SizeOf<Td2dVertex>())), " +
                "0x08u, 0x0142u, 0, out f_VB) < 0)";
        }

        if (method.Equals("CreateIndexBuffer", StringComparison.OrdinalIgnoreCase) &&
            destination.Equals("f_IB", StringComparison.OrdinalIgnoreCase) &&
            bufferCall.Children[3].Text.Equals("D3DFMT_INDEX16", StringComparison.OrdinalIgnoreCase) &&
            length == "((VertexBufferSize * (6 / 4)) * SizeOf(Word))")
        {
            return "(f_D3DDevice.CreateIndexBuffer(" +
                "checked((uint)((VertexBufferSize * 6 / 4) * sizeof(ushort))), " +
                "0x08u, 101, 0, out f_IB) < 0)";
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase))
    {
        var lockCall = expression;
        bool checkLockResult =
            _resolver is not null &&
            targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            targetExpression.Text.Equals("Failed", StringComparison.OrdinalIgnoreCase) &&
            ResolveExpressionType(targetExpression) is null &&
            !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName!) &&
            expression.Children.Count == 2 &&
            expression.Children[1].Kind == ExpressionParser.ExpressionKind.Call;

        if (checkLockResult)
            lockCall = expression.Children[1];

        if (lockCall.Children.Count == 5 &&
            lockCall.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } lockTarget &&
            lockTarget.Children.Count == 2 &&
            lockTarget.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            lockTarget.Children[1].Text.Equals("Lock", StringComparison.OrdinalIgnoreCase))
        {
            string buffer = lockTarget.Children[0].Text;
            string lockAddress = TranslateExpression(lockCall.Children[3]);
            string? destination = null;

            if (buffer.Equals("f_VB", StringComparison.OrdinalIgnoreCase) &&
                lockAddress.Equals("PByte(f_VertArray)", StringComparison.OrdinalIgnoreCase))
            {
                buffer = "f_VB";
                destination = "f_VertArray";
            }
            else if (buffer.Equals("f_IB", StringComparison.OrdinalIgnoreCase) &&
                lockAddress.Equals("PByte(l_Index)", StringComparison.OrdinalIgnoreCase))
            {
                buffer = "f_IB";
                destination = "l_Index";
            }

            if (destination is not null)
            {
                string offset = TranslateExpression(lockCall.Children[1]);
                string size = TranslateExpression(lockCall.Children[2]);
                string flags = TranslateExpression(lockCall.Children[4]);
                string call = $"{buffer}.Lock({offset}, {size}, out {destination}, {flags})";
                return checkLockResult ? $"({call} < 0)" : call;
            }
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[0].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase))
    {
        string streamMethod = targetExpression.Children[1].Text;
        if (streamMethod.Equals("SetStreamSource", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 4 &&
            TranslateExpression(expression.Children[3]) == "SizeOf(Td2dVertex)")
        {
            string streamNumber = TranslateExpression(expression.Children[1]);
            string vertexBuffer = TranslateExpression(expression.Children[2]);
            return "global::FireURQ.Runtime.D3D8.SetStreamSource(f_D3DDevice, " +
                $"checked((uint)({streamNumber})), {vertexBuffer}, " +
                "checked((uint)global::System.Runtime.InteropServices.Marshal.SizeOf<Td2dVertex>()))";
        }

        if (streamMethod.Equals("SetIndices", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 3)
        {
            string indexBuffer = TranslateExpression(expression.Children[1]);
            string baseVertex = TranslateExpression(expression.Children[2]);
            return "global::FireURQ.Runtime.D3D8.SetIndices(f_D3DDevice, " +
                $"{indexBuffer}, checked((uint)({baseVertex})))";
        }
    }

    if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[0].Text.Equals("f_D3DDevice", StringComparison.OrdinalIgnoreCase))
    {
        string drawMethod = targetExpression.Children[1].Text.ToUpperInvariant();
        var drawArgs = expression.Children.Skip(1).ToArray();
        const string d3dRuntime = "global::FireURQ.Runtime.D3D8.";

        if (drawMethod == "CLEAR" && drawArgs.Length == 6 &&
            IsNilLiteral(drawArgs[1]) && TranslateExpression(drawArgs[4]) == "1.0")
        {
            string clearFlags = TranslateExpression(drawArgs[2])
                .Replace("D3DCLEAR_TARGET", "1u", StringComparison.OrdinalIgnoreCase)
                .Replace("D3DCLEAR_ZBUFFER", "2u", StringComparison.OrdinalIgnoreCase);
            return "f_D3DDevice.Clear(" +
                $"{TranslateExpression(drawArgs[0])}, global::System.IntPtr.Zero, {clearFlags}, " +
                $"{TranslateExpression(drawArgs[3])}, 1.0f, {TranslateExpression(drawArgs[5])})";
        }

        if (drawMethod == "SETVERTEXSHADER" && drawArgs.Length == 1 &&
            drawArgs[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            drawArgs[0].Text.Equals("D3DFVF_D2DVERTEX", StringComparison.OrdinalIgnoreCase))
        {
            return d3dRuntime + "SetVertexShader(f_D3DDevice, 0x0142u)";
        }

        if (((drawMethod == "DRAWPRIMITIVE" && drawArgs.Length == 3) ||
             (drawMethod == "DRAWINDEXEDPRIMITIVE" && drawArgs.Length == 5)) &&
            drawArgs[0].Kind == ExpressionParser.ExpressionKind.Identifier)
        {
            string? primitiveType = drawArgs[0].Text.ToUpperInvariant() switch
            {
                "D3DPT_LINELIST" => "2",
                "D3DPT_TRIANGLELIST" => "4",
                _ => null
            };
            if (primitiveType is not null)
            {
                string name = drawMethod == "DRAWPRIMITIVE" ? "DrawPrimitive" : "DrawIndexedPrimitive";
                string counts = string.Join(", ", drawArgs.Skip(1)
                    .Select(arg => $"checked((uint)({TranslateExpression(arg)}))"));
                return $"{d3dRuntime}{name}(f_D3DDevice, {primitiveType}, {counts})";
            }
        }
    }

    bool isSystemLog =
        (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
         string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
         targetExpression.Text.Equals("System_Log", StringComparison.OrdinalIgnoreCase)) ||
        (targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
         targetExpression.Children.Count == 2 &&
         targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
         targetExpression.Children[0].Text.Equals("gD2DE", StringComparison.OrdinalIgnoreCase) &&
         targetExpression.Children[1].Text.Equals("System_Log", StringComparison.OrdinalIgnoreCase));

    if (isSystemLog && expression.Children.Count == 3 &&
        expression.Children[2].Kind == ExpressionParser.ExpressionKind.Set &&
        !expression.Children[2].Children.Any(element =>
            element.Kind == ExpressionParser.ExpressionKind.Binary && element.Text == ".."))
    {
        string format = TranslateExpression(expression.Children[1]);
        string values = string.Join(", ",
            expression.Children[2].Children.Select(element =>
            {
                if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                    element.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
                    element.Children.Count == 2 &&
                    MapType(ResolveExpressionType(element.Children[0])) == "D3DADAPTER_IDENTIFIER8")
                {
                    string adapter = TranslateExpression(element.Children[0]);
                    string field = element.Children[1].Text;
                    if (field.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                        return $"{adapter}.DriverText";
                    if (field.Equals("Description", StringComparison.OrdinalIgnoreCase))
                        return $"{adapter}.DescriptionText";
                }

                string value = TranslateExpression(element);
                if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                    element.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
                    element.Children.Count == 2 &&
                    element.Children[0].Text.Equals("l_DevInfo", StringComparison.OrdinalIgnoreCase) &&
                    element.Children[1].Text.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                    MapType(ResolveExpressionType(element.Children[0])) == "BASS_DEVICEINFO")
                {
                    return $"global::System.Runtime.InteropServices.Marshal.PtrToStringAnsi({value}) ?? string.Empty";
                }

                return value;
            }));
        string method = targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier
            ? "System_Log"
            : $"{TranslateExpression(targetExpression.Children[0])}.System_Log";
        return $"{method}({format}, new object[] {{ {values} }})";
    }

    if (TryTranslateStreamCall(expression, targetExpression, out string streamCall))
        return streamCall;

    if (expression.Children.Count == 1 &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[1].Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Children[1].Text.Equals(
            "Free", StringComparison.OrdinalIgnoreCase))
    {
        ExpressionParser.ExpressionNode receiver = targetExpression.Children[0];
        string? receiverType = ResolveExpressionType(receiver);
        if (receiverType is not null &&
            _resolver is not null &&
            _resolver.IsInheritedTObjectFree(receiverType, _unitName))
        {
            return "global::FireURQ.Runtime.TObject.Free(" +
                TranslateExpression(receiver) + ")";
        }
    }

    string target =
        TranslateCallTarget(
            targetExpression);

    var arguments = new List<string>();

    for (int i = 1;
         i < expression.Children.Count;
         i++)
    {
        arguments.Add(
            TranslateExpression(
                expression.Children[i]));
    }

    // Delphi Assigned(P) is a null/non-null test, including the common
    // Assigned(@ProcedureVariable) spelling used after GetProcAddress.
    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals(
            "Assigned",
            StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 2)
    {
        ExpressionParser.ExpressionNode argument =
            UnwrapAddressOfIdentifier(expression.Children[1]);

        string assignedValue = TranslateExpression(argument);
        string nullValue = IsIntPtrBackedExpression(argument)
            ? "IntPtr.Zero"
            : "null";
        return $"({assignedValue} != {nullValue})";
    }

if (targetExpression.Kind ==
        ExpressionParser.ExpressionKind.Identifier &&
    targetExpression.Text.Equals(
        "Inc",
        StringComparison.OrdinalIgnoreCase))
{
    if (arguments.Count == 1)
    {
        return $"{arguments[0]}++";
    }

    if (arguments.Count == 2)
    {
        return $"{arguments[0]} += {arguments[1]}";
    }
}

    if (targetExpression.Kind ==
            ExpressionParser.ExpressionKind.Identifier &&
        IsBuiltInDelphiType(
            targetExpression.Text) &&
        arguments.Count == 1)
    {
        string csharpType =
            MapType(
                targetExpression.Text);

        return $"({csharpType})({arguments[0]})";
    }

    return $"{target}({string.Join(", ", arguments)})";
}



private static bool IsBuiltInDelphiType(
    string typeName)
{
    return typeName.Equals(
               "Integer",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Boolean",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "String",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Real",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Double",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Single",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Char",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Byte",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Word",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Cardinal",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "LongWord",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "Int64",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "LongInt",
               StringComparison.OrdinalIgnoreCase) ||
           typeName.Equals(
               "ShortInt",
               StringComparison.OrdinalIgnoreCase);
}

    private string TranslateTypeTest(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        string left =
            TranslateExpression(
                expression.Children[0]);

        string right =
            TranslateExpression(
                expression.Children[1]);

        return $"({left} is {right})";
    }

    private string TranslateTypeCast(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        string left =
            TranslateExpression(
                expression.Children[0]);

        string right =
            TranslateExpression(
                expression.Children[1]);

        return $"({left} as {right})";
    }

    private string TranslateSet(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count == 0)
            return "new HashSet<int>()";

        var scalarElements = new List<string>();
        var ranges = new List<string>();

        foreach (ExpressionParser.ExpressionNode element in expression.Children)
        {
            if (element.Kind == ExpressionParser.ExpressionKind.Binary &&
                string.Equals(element.Text, "..", StringComparison.Ordinal) &&
                element.Children.Count >= 2)
            {
                string lower = TranslateExpression(element.Children[0]);
                string upper = TranslateExpression(element.Children[1]);

                ranges.Add(
                    $"Enumerable.Range((int)({lower}), (int)({upper}) - (int)({lower}) + 1)");
                continue;
            }

            string value = TranslateExpression(element);
            scalarElements.Add($"(int)({value})");
        }

        string values = scalarElements.Count > 0
            ? $"new int[] {{ {string.Join(", ", scalarElements)} }}"
            : "Enumerable.Empty<int>()";

        foreach (string range in ranges)
            values += $".Concat({range})";

        return $"new HashSet<int>({values})";
    }

    private string TranslateSetMembership(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        string value =
            TranslateExpression(
                expression.Children[0]);

        ExpressionParser.ExpressionNode set =
            expression.Children[1];

        if (set.Kind !=
            ExpressionParser.ExpressionKind.Set)
        {
            // Named Pascal set types are translated to HashSet<int>
            // aliases, so membership must call Contains rather than
            // emitting the Pascal "in" operator into C#.
            string translatedSet = TranslateExpression(set);
            return $"{translatedSet}.Contains((int)({value}))";
        }

        var conditions =
            new List<string>();

        foreach (
            ExpressionParser.ExpressionNode element
            in set.Children)
        {
            if (element.Kind ==
                    ExpressionParser.ExpressionKind.Binary &&
                string.Equals(
                    element.Text,
                    "..",
                    StringComparison.Ordinal))
            {
                if (element.Children.Count >= 2)
                {
                    string lower =
                        TranslateExpression(
                            element.Children[0]);

                    string upper =
                        TranslateExpression(
                            element.Children[1]);

                    conditions.Add(
                        $"({value} >= {lower} && {value} <= {upper})");
                }

                continue;
            }

            string translatedElement =
                TranslateExpression(element);

            conditions.Add(
                $"({value} == {translatedElement})");
        }

        return conditions.Count == 0
            ? "false"
            : $"({string.Join(" || ", conditions)})";
    }

private string TranslateLiteral(
    string text)
{
    string value = text.Trim();

    if (TryTranslatePascalStringLiteral(value, out string literal))
        return literal;

    if (value.StartsWith("$", StringComparison.Ordinal))
    {
        string hexadecimal = value[1..];
        if (hexadecimal.Length > 0 && hexadecimal.All(c => Uri.IsHexDigit(c)))
            return $"0x{hexadecimal}";
    }

    if (value.Equals("nil", StringComparison.OrdinalIgnoreCase))
        return "null";

    if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("false", StringComparison.OrdinalIgnoreCase))
        return value.ToLowerInvariant();

    return value;
}

private static bool TryTranslatePascalStringLiteral(string text, out string result)
{
    result = string.Empty;
    if (text.Length == 0 || (text[0] != '#' && text[0] != '\''))
        return false;

    var characters = new List<char>();
    int position = 0;
    while (position < text.Length)
    {
        if (char.IsWhiteSpace(text[position])) { position++; continue; }
        if (text[position] == '#')
        {
            position++;
            int digitStart = position;
            while (position < text.Length && char.IsDigit(text[position])) position++;
            if (digitStart == position ||
                !int.TryParse(text[digitStart..position], out int code) ||
                code < 0 || code > char.MaxValue)
                return false;
            characters.Add((char)code);
            continue;
        }

        if (text[position] == '\'')
        {
            position++;
            bool closed = false;
            while (position < text.Length)
            {
                char current = text[position++];
                if (current == '\'')
                {
                    if (position < text.Length && text[position] == '\'')
                    {
                        characters.Add('\'');
                        position++;
                        continue;
                    }
                    closed = true;
                    break;
                }
                characters.Add(current);
            }
            if (!closed) return false;
            continue;
        }
        return false;
    }

    // In Pascal, '' is the empty string. Do not leave it unchanged:
    // C# interprets '' as an invalid empty character literal.
    if (characters.Count == 0)
    {
        if (text == "''")
        {
            result = "\"\"";
            return true;
        }

        return false;
    }

    if (text[0] == '#' && characters.Count == 1)
    {
        result = "'" + EscapeCSharpCharacter(characters[0]) + "'";
        return true;
    }
    result = "\"" + EscapeCSharpString(new string(characters.ToArray())) + "\"";
    return true;
}

private static string EscapeCSharpCharacter(char value)
{
    return value switch
    {
        '\\' => "\\\\",
        '\'' => "\\'",
        '\0' => "\\0",
        '\a' => "\\a",
        '\b' => "\\b",
        '\f' => "\\f",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\v' => "\\v",
        _ when char.IsControl(value) => "\\u" + ((int)value).ToString("X4"),
        _ => value.ToString()
    };
}

private static string EscapeCSharpString(string value)
{
    var builder = new StringBuilder();
    foreach (char character in value)
    {
        builder.Append(character switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ when char.IsControl(character) => "\\u" + ((int)character).ToString("X4"),
            _ => character.ToString()
        });
    }
    return builder.ToString();
}

private string TranslateAddressOf(ExpressionParser.ExpressionNode expression)
{
    if (expression.Children.Count == 0)
        return "default /* TODO: Delphi address-of operand missing */";

    // Delphi address-of has no general safe C# equivalent. Preserve the operand
    // and flag pointer/API adaptation rather than emitting a bare '@'.
    return "/* TODO: Delphi address-of */ " + TranslateExpression(expression.Children[0]);
}

    private static string MapParameterKind(
        string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "var" => "ref ",
            "out" => "out ",
            "const" => "in ",
            _ => string.Empty
        };
    }

    private static void AppendLine(
        StringBuilder builder,
        int indent,
        string text)
    {
        builder
            .Append(
                new string(
                    ' ',
                    indent * 4))
            .AppendLine(text);
    }

    private sealed class FunctionTypeContext
    {
        private readonly Dictionary<string, string> _identifiers =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly string _returnType;
        private readonly FunctionTypeContext? _parent;
        private readonly int _depth;
        public string ResultName { get; }
        public Dictionary<string, string> ConstCopies { get; } = new(StringComparer.OrdinalIgnoreCase);

        public FunctionTypeContext(SemanticFunction function, FunctionTypeContext? parent)
        {
            _parent = parent;
            _depth = parent is null ? 0 : parent._depth + 1;
            ResultName = _depth == 0 ? "result" : $"__localResult{_depth}";
            foreach (
                SemanticParameter parameter
                in function.Parameters)
            {
                _identifiers[parameter.Name] =
                    parameter.Type;
            }

            foreach (
                SemanticVariable variable
                in function.LocalVariables)
            {
                _identifiers[variable.Name] =
                    variable.Type;
            }

            if (function.LocalFunctions.Count != 0)
            {
                foreach (var parameter in function.Parameters)
                {
                    if (!parameter.Kind.Equals("const", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string copy = $"__captured{_depth}_{parameter.Name}";
                    while (_identifiers.ContainsKey(copy) || parent?.ResolveIdentifier(copy) is not null)
                        copy += "_";
                    ConstCopies.Add(parameter.Name, copy);
                    _identifiers.Add(copy, parameter.Type);
                }
            }

            _returnType = function.ReturnType;
        }

        public string? ResolveIdentifier(
            string name)
        {
            if (name.Equals(
                    "Result",
                    StringComparison.OrdinalIgnoreCase))
            {
                return _returnType;
            }

            return _identifiers.TryGetValue(
                name,
                out string? type)
                ? type
                : _parent?.ResolveIdentifier(name);
        }

        public string ResolveSpelling(string name)
        {
            if (_identifiers.ContainsKey(name))
                return name;
            return _parent?.ResolveCapturedSpelling(name) ?? name;
        }

        private string ResolveCapturedSpelling(string name) =>
            ConstCopies.TryGetValue(name, out string? copy) ? copy : ResolveSpelling(name);
    }
}
