using System.Text;
using FireURQ.Porter.Analysis;
using FireURQ.Porter.AST;

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

    private bool IsSimpleXmlVariantFormatter(SemanticFunction function) =>
        string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
        function.Name.Equals("VarToXSTR", StringComparison.OrdinalIgnoreCase) &&
        function.Parameters.Count == 1 &&
        function.Parameters[0].Type.Equals("TVarData", StringComparison.OrdinalIgnoreCase);

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
        bool manageDestructorLifetime = false, string? ownerClass = null) =>
        TranslateFunctionCore(function, behavior, manageDestructorLifetime, null, ownerClass);

    private string TranslateFunctionCore(
        SemanticFunction function, BehaviorAnalyzer.BehaviorInfo behavior,
        bool manageDestructorLifetime, FunctionTypeContext? parent, string? ownerClass = null)
    {
        FunctionTypeContext? previous =
            _currentTypeContext;

        _currentTypeContext =
            new FunctionTypeContext(function, parent, ownerClass);
        FunctionTypeContext context = _currentTypeContext;

        try
        {
            var builder = new StringBuilder();
            bool isSimpleXmlVariantFormatter = IsSimpleXmlVariantFormatter(function);

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
                    .Append(isLogArgumentArray ? "object[]" :
                        isSimpleXmlVariantFormatter ? "Variant" : MapType(parameter.Type))
                    .Append(' ')
                    .Append(parameter.Name);
                string declaredDefault = DefaultParameterSuffix(function.Parameters, i, t => MapType(t));
                if (declaredDefault.Length > 0)
                {
                    builder.Append(declaredDefault);
                    continue;
                }

                builder
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

                // Preserve the optional Resource_Load flags.
                if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
                    function.Name.Equals("Resource_Load", StringComparison.OrdinalIgnoreCase) &&
                    function.Parameters.Count == 4 &&
                    i >= 2 &&
                    MapType(parameter.Type) == "bool")
                {
                    builder.Append(" = false");
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

            if (string.Equals(_unitName, "furqTypes", StringComparison.OrdinalIgnoreCase) &&
                function.Name.Equals("EnsureReal", StringComparison.OrdinalIgnoreCase) &&
                function.Parameters.Count == 1 &&
                function.Parameters[0].Type.Equals("Variant", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine($"    return Extended.EnsureReal({function.Parameters[0].Name});");
                builder.AppendLine("}");
                return builder.ToString();
            }

            if (isSimpleXmlVariantFormatter)
            {
                string value = function.Parameters[0].Name;
                builder.AppendLine($$"""
    return {{value}}.TypeCode switch
    {
        1 => XSTR_NULL,
        2 or 3 or 16 or 17 or 18 or 19 or 20 =>
            global::System.Convert.ToString({{value}}.BoxedValue, global::System.Globalization.CultureInfo.InvariantCulture) ?? "",
        4 or 5 or 6 => FloatToXSTR((double){{value}}),
        7 => DateTimeToXSTR((double){{value}}),
        11 => {{value}}.TryGetBoolean(out bool flag) && flag ? "1" : "0",
        0x2011 => global::System.Convert.ToBase64String((byte[]){{value}}.BoxedValue!),
        _ => (string){{value}}
    };
""");
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
                    string localType = MapType(variable.Type);
                    string initializer = "default!";
                    // A zero-based static array owns storage from procedure entry.
                    var fixedArray = System.Text.RegularExpressions.Regex.Match(variable.Type,
                        @"^array\s*\[\s*0\s*\.\.\s*(\d+)\s*\]\s*of\s+(.+)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (fixedArray.Success && int.TryParse(fixedArray.Groups[1].Value, out int upper)
                        && upper < int.MaxValue && localType.EndsWith("[]", StringComparison.Ordinal)
                        && !localType[..^2].Contains('['))
                        initializer = $"new {localType[..^2]}[{upper + 1}]";
                    builder.AppendLine($"    {localType} {variable.Name} = {initializer};");
                }
            }

            if (!function.IsProcedure)
            {
                builder.AppendLine(
                    $"    {MapType(function.ReturnType)} {context.ResultName} = default!;");
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

    private readonly Stack<(string Alias, string Type)> _withScopes = new();

    private ExpressionParser.ExpressionNode BindWithExpression(ExpressionParser.ExpressionNode node, int? arity = null)
    {
        if (node.Kind == ExpressionParser.ExpressionKind.Identifier && _resolver is not null)
        {
            foreach (var scope in _withScopes)
            {
                var member = _resolver.FindMember(scope.Type, node.Text, _unitName, arity);
                string? name = member?.Member.Name;
                if (name is null && arity is null)
                    name = _resolver.FindRecordField(scope.Type, node.Text, _unitName)?.Name
                        ?? _resolver.FindMember(scope.Type, node.Text, _unitName, 0)?.Member.Name;
                if (name is null)
                    continue;
                return new ExpressionParser.ExpressionNode(ExpressionParser.ExpressionKind.MemberAccess, "",
                    new[] { node with { Text = scope.Alias }, node with { Text = name } });
            }
            return node;
        }
        if (node.Children.Count == 0)
            return node;
        return node with { Children = node.Children.Select((child, index) =>
            node.Kind == ExpressionParser.ExpressionKind.MemberAccess && index == 1 ? child :
            BindWithExpression(child, node.Kind == ExpressionParser.ExpressionKind.Call && index == 0
                ? node.Children.Count - 1 : null)).ToArray() };
    }

    private void TranslateWith(BehaviorAnalyzer.BehaviorStatement statement, SemanticFunction function,
        StringBuilder builder, int indent)
    {
        if (statement.Expression is not { } receiver || _currentTypeContext is null)
            throw new InvalidOperationException("Missing Delphi with receiver.");
        string? type = ResolveExpressionType(receiver) ?? ResolveUnaryMemberType(receiver)?.ResolvedName
            ?? ResolveUnaryFunctionTarget(receiver, 0)?.Type.ResolvedName;
        string alias = "__with" + _withScopes.Count;
        while (_currentTypeContext.ResolveIdentifier(alias) is not null)
            alias += "_";
        string target = TranslateValueOperand(receiver, out _);
        bool recordVariable =
            receiver.Kind is ExpressionParser.ExpressionKind.Identifier or ExpressionParser.ExpressionKind.IndexAccess &&
            type is not null &&
            _resolver?.ResolveDeclarationKind(type, _unitName) != "Class" &&
            _resolver?.ResolveDeclarationKind(type, _unitName) != "Interface" &&
            (ResolveExpressionType(receiver) is not null ||
             (_currentTypeContext.OwnerClass is { } owner &&
              _resolver?.FindMember(owner, receiver.Text, _unitName, null)?.Member.Kind == SemanticMemberKind.Field));
        AppendLine(builder, indent, "{");
        if (receiver.Kind == ExpressionParser.ExpressionKind.Dereference && receiver.Children.Count == 1 &&
            _resolver is not null &&
            (ResolveExpressionType(receiver.Children[0]) ?? ResolveUnaryMemberType(receiver.Children[0])?.ResolvedName) is string pointerName &&
            _resolver.Resolve(pointerName, _unitName) is { IsResolved: true, PointerDepth: 1 } pointer &&
            _resolver.FixedScalarRecordSize(pointer.ResolvedName, pointer.DeclaringUnitName) > 0)
        {
            type = pointer.ResolvedName;
            target = $"global::FireURQ.Runtime.DelphiSystem.Dereference<{type}>({target})";
            recordVariable = true;
        }
        AppendLine(builder, indent + 1, recordVariable ? $"ref var {alias} = ref {target};" : $"var {alias} = {target};");
        _currentTypeContext.AddWithAlias(alias, type ?? "object");
        _withScopes.Push((alias, type ?? "object"));
        try
        {
            foreach (var child in statement.Children)
                TranslateStatement(child, function, builder, indent + 1);
        }
        finally
        {
            _withScopes.Pop();
            _currentTypeContext.RemoveWithAlias(alias);
        }
        AppendLine(builder, indent, "}");
    }

    private void TranslateStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
        if (_withScopes.Count > 0)
        {
            statement = statement with
            {
                Expression = statement.Expression is { } expression ? BindWithExpression(expression) : null,
                Assignment = statement.Assignment is { } assignment ? assignment with
                {
                    Target = BindWithExpression(assignment.Target),
                    Value = BindWithExpression(assignment.Value)
                } : null,
                ForLoop = statement.ForLoop is { } loop ? loop with
                {
                    From = BindWithExpression(loop.From), To = BindWithExpression(loop.To)
                } : null
            };
        }
        if (statement.Kind == BehaviorAnalyzer.StatementKind.With)
        {
            TranslateWith(statement, function, builder, indent);
            return;
        }
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
        string condition = TranslateCondition(statement.Expression);

        if (statement.Expression?.Kind == ExpressionParser.ExpressionKind.Binary &&
            condition.StartsWith("(") && condition.EndsWith(")"))
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
                : TranslateCondition(statement.Expression);

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
                var fallback = new List<BehaviorAnalyzer.BehaviorStatement>();
                foreach (var handler in clause.Statements)
                {
                    if (!handler.Text.StartsWith("on ", StringComparison.OrdinalIgnoreCase))
                    {
                        fallback.Add(handler);
                        continue;
                    }

                    string header = handler.Text[3..].Trim();
                    int separator = header.IndexOf(':');
                    string type = handler.SecondaryText ?? header;
                    string binding = separator < 0 ? "" : " " + header[..separator].Trim();
                    AppendLine(builder, indent, $"catch ({MapType(type)}{binding})");
                    AppendLine(builder, indent, "{");
                    foreach (var child in handler.Children)
                        TranslateStatement(child, function, builder, indent + 1);
                    AppendLine(builder, indent, "}");
                }

                if (fallback.Count > 0 || clause.Statements.Count == 0)
                {
                    AppendLine(builder, indent, "catch");
                    AppendLine(builder, indent, "{");
                    foreach (var child in fallback)
                        TranslateStatement(child, function, builder, indent + 1);
                    AppendLine(builder, indent, "}");
                }
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
        string condition = TranslateCondition(statement.Expression);

        if (statement.Expression?.Kind == ExpressionParser.ExpressionKind.Binary &&
            condition.StartsWith("(") && condition.EndsWith(")"))
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
            TranslateValueOperand(loop.From, out _);

        string to =
            TranslateValueOperand(loop.To, out _);

        string variable = TranslateIdentifier(loop.Variable);

        bool descending = loop.Direction.Equals("downto", StringComparison.OrdinalIgnoreCase);
        string comparison = descending ? ">=" : "<=";
        string increment = descending ? "--" : "++";

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

        if (text.Equals("MaxInt", StringComparison.OrdinalIgnoreCase) &&
            _resolver is not null && _unitName is not null &&
            _currentTypeContext?.ResolveIdentifier(text) is null &&
            (_currentTypeContext?.OwnerClass is not { } owner ||
                _resolver.ResolveDataMemberType(owner, text, _unitName) is null) &&
            !_resolver.HasVisibleValueDeclaration(text, _unitName))
            return "int.MaxValue";

        if (text.Equals("Self", StringComparison.OrdinalIgnoreCase) &&
            _currentTypeContext?.OwnerClass is not null)
            return "this";

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

        if (_currentTypeContext?.ResolveIdentifier(text) is null &&
            _currentTypeContext?.OwnerClass is { } ownerType &&
            _resolver?.FindMember(ownerType, text, _unitName, null) is { } dataMember)
            return dataMember.Member.Name;

        if (_unitName is not null && _resolver is not null &&
            _currentTypeContext?.ResolveIdentifier(text) is null &&
            _currentTypeContext?.ResolveLocalFunction(text, 0, valueOnly: false).Found != true &&
            _resolver.TryResolveConstant(text, _unitName, out var constantDeclaration, out _) &&
            constantDeclaration is not null)
            return constantDeclaration.Name;

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

        if (TryTranslateIndexedPropertyWrite(
            statement.Assignment.Target,
            statement.Assignment.Value,
            out string indexedWrite))
        {
            AppendLine(builder, indent, indexedWrite);
            return;
        }

        if (statement.Assignment.Target is { Kind: ExpressionParser.ExpressionKind.IndexAccess } character &&
            character.Children.Count == 2 && IsManagedString(character.Children[0]))
        {
            string storage = TranslateExpression(character.Children[0]);
            string index = TranslateValueOperand(character.Children[1], out _);
            string replacement = TranslateExpectedValue(statement.Assignment.Value, "Char");
            AppendLine(builder, indent, $"global::FireURQ.Runtime.DelphiStorage.SetChar(ref {storage}, {index}, {replacement});");
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
            // Dynamic_Bass declares each BASS export as a procedural variable.
            // The declaration generator names its delegate after the variable.
            var args = statement.Assignment.Value.Children;
            if (string.Equals(_unitName, "Dynamic_Bass", StringComparison.OrdinalIgnoreCase) &&
                function.Name.Equals("Load_BASSDLL", StringComparison.OrdinalIgnoreCase) &&
                args.Count == 3 && args[1].Kind == ExpressionParser.ExpressionKind.Identifier &&
                args[1].Text.Equals("BASS_Handle", StringComparison.OrdinalIgnoreCase) &&
                args[2].Kind == ExpressionParser.ExpressionKind.Call &&
                args[2].Children.Count == 2 &&
                args[2].Children[0].Text.Equals("PAnsiChar", StringComparison.OrdinalIgnoreCase) &&
                args[2].Children[1].Kind == ExpressionParser.ExpressionKind.Literal)
            {
                string export = TranslateExpression(args[2].Children[1]);
                string field = targetExpression.Text;
                if (export == $"\"{field}\"")
                {
                    AppendLine(builder, indent, "{");
                    AppendLine(builder, indent + 1,
                        $"{field} = NativeLibrary.TryGetExport(BASS_Handle, {export}, out var address)");
                    AppendLine(builder, indent + 2,
                        $"? Marshal.GetDelegateForFunctionPointer<{field}Delegate>(address) : null!;");
                    AppendLine(builder, indent, "}");
                    return;
                }
            }
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

        string target;
        bool previousAssignmentTarget = _translatingAssignmentTarget;
        _translatingAssignmentTarget = true;
        try
        {
            target = TranslateExpression(targetExpression);
        }
        finally
        {
            _translatingAssignmentTarget = previousAssignmentTarget;
        }

        string value =
            IsNilLiteral(statement.Assignment.Value) &&
            IsIntPtrBackedExpression(targetExpression)
                ? "IntPtr.Zero"
                : TranslateExpectedValue(statement.Assignment.Value, ResolveExpressionType(targetExpression)
                    ?? ResolveUnaryMemberType(targetExpression)?.ResolvedName);

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

        // Resource_Load writes a Longword through its PLongword argument.
        if (string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase) &&
            function.Name.Equals("Resource_Load", StringComparison.OrdinalIgnoreCase) &&
            targetExpression.Kind == ExpressionParser.ExpressionKind.Dereference &&
            targetExpression.Children.Count == 1 &&
            targetExpression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            targetExpression.Children[0].Text.Equals("aSize", StringComparison.OrdinalIgnoreCase))
        {
            AppendLine(builder, indent,
                $"global::System.Runtime.InteropServices.Marshal.WriteInt32(aSize, unchecked((int)({value})));" );
            return;
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

    private bool TryGetConstructorFactory(ExpressionParser.ExpressionNode target, int arity,
        out string factory, out IReadOnlyList<SemanticParameter>? parameters)
    {
        factory = string.Empty;
        parameters = null;
        if (_resolver is null || target.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
            target.Children.Count != 2 || target.Children[0].Kind != ExpressionParser.ExpressionKind.Identifier ||
            ResolveExpressionType(target.Children[0]) is not null || ResolveUnaryMemberType(target.Children[0]) is not null)
            return false;

        var type = _resolver.Resolve(target.Children[0].Text, _unitName);
        if (type.IsPointer || type.DeclaringUnitName is null ||
            ProjectTranslator.UsesManagedStandaloneClass(type.DeclaringUnitName, type.ResolvedName) ||
            _resolver.FindMember(type.ResolvedName, target.Children[1].Text, type.DeclaringUnitName, arity, constructor: true) is not { } binding)
            return false;

        factory = $"{type.ResolvedName}.__New_{binding.Member.Name}<{type.ResolvedName}>";
        parameters = binding.Member.Parameters;
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
    private bool IsSimpleXmlBareCall(ExpressionParser.ExpressionNode expression) =>
        string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
        expression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        expression.Children.Count == 2 &&
        ((expression.Children[0].Text.Equals("aReader", StringComparison.OrdinalIgnoreCase) &&
          expression.Children[1].Text.ToUpperInvariant() is
              "READLONGINT" or "READXMLSTRING" or "READANSISTRING" or "READWIDESTRING") ||
         (expression.Children[0].Text.Equals("aWriter", StringComparison.OrdinalIgnoreCase) &&
          expression.Children[1].Text.Equals("FlushBuf", StringComparison.OrdinalIgnoreCase)));

    private string TranslateExpression(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null)
            return string.Empty;

        if (TryGetStandardListFactoryTarget(expression, out string factoryTarget))
            return factoryTarget + "()";

        if (TryGetConstructorFactory(expression, 0, out string constructorFactory, out _))
            return constructorFactory + "()";

        bool isFontMemberCall =
            string.Equals(_unitName, "f2FontGen", StringComparison.OrdinalIgnoreCase) &&
            expression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
            expression.Children.Count == 2 &&
            expression.Children[1].Text.ToUpperInvariant() is "GETWIDTH" or "GETHEIGHT" or "FIRST" or "NEXT";
        if (IsF2LinkParserBareCall(expression) || IsSimpleXmlBareCall(expression) || isFontMemberCall)
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

        string left = TranslateValueOperand(expression.Children[0], out bool leftBoolean);
        string right = TranslateValueOperand(expression.Children[1], out bool rightBoolean);

        if (string.Equals(
                expression.Text,
                "in",
                StringComparison.OrdinalIgnoreCase))
        {
            return TranslateSetMembership(expression);
        }

        var lhs = expression.Children[0];
        var rhs = expression.Children[1];
        string? leftType = ResolveExpressionType(lhs) ?? ResolveUnaryMemberType(lhs)?.ResolvedName;
        string? rightType = ResolveExpressionType(rhs) ?? ResolveUnaryMemberType(rhs)?.ResolvedName;
        if (expression.Text is "=" or "<>" or "<" or ">" or "<=" or ">=")
        {
            if (expression.Text is "=" or "<>")
            {
                static bool IsAnsiBuffer(string? type) =>
                    type is not null && System.Text.RegularExpressions.Regex.IsMatch(type,
                        @"^array\s*\[\s*0\s*\.\.[^\]]+\]\s*of\s+AnsiChar$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (IsAnsiBuffer(leftType) && (MapType(rightType) == "string" ||
                    rhs.Kind == ExpressionParser.ExpressionKind.Literal && right.StartsWith('"')))
                    left = $"global::FireURQ.Runtime.DelphiText.FromAnsiCharArray({left})";
                if (IsAnsiBuffer(rightType) && (MapType(leftType) == "string" ||
                    lhs.Kind == ExpressionParser.ExpressionKind.Literal && left.StartsWith('"')))
                    right = $"global::FireURQ.Runtime.DelphiText.FromAnsiCharArray({right})";
            }
            if (lhs.Kind == ExpressionParser.ExpressionKind.Literal && MapType(rightType) == "char")
                left = TranslateExpectedValue(lhs, "Char");
            if (rhs.Kind == ExpressionParser.ExpressionKind.Literal && MapType(leftType) == "char")
                right = TranslateExpectedValue(rhs, "Char");
        }

        bool isBoolean =
            leftBoolean || rightBoolean || IsBooleanType(leftType) || IsBooleanType(rightType) ||
            (expression.Text.ToLowerInvariant() is "and" or "or" &&
             (IsBooleanUnaryOperand(lhs) || IsBooleanUnaryOperand(rhs)));

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

        if (expression.Text == "/" && IsOrdinalArithmetic(lhs) && IsOrdinalArithmetic(rhs))
            return $"(global::FireURQ.Runtime.Extended.FromInt64({left}) / global::FireURQ.Runtime.Extended.FromInt64({right}))";

        return $"({left} {op} {right})";
    }

    private bool IsOrdinalArithmetic(ExpressionParser.ExpressionNode value)
    {
        if (value.Kind == ExpressionParser.ExpressionKind.Literal)
            return long.TryParse(value.Text, out _) ||
                (value.Text.StartsWith('$') && ulong.TryParse(value.Text.AsSpan(1),
                    System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture,
                    out ulong hexadecimal) && hexadecimal <= long.MaxValue);

        if (value.Kind == ExpressionParser.ExpressionKind.Unary &&
            value.Children.Count == 1 && value.Text is "+" or "-")
            return IsOrdinalArithmetic(value.Children[0]);
        if (value.Kind == ExpressionParser.ExpressionKind.Binary && value.Children.Count == 2 &&
            value.Text.ToLowerInvariant() is "+" or "-" or "*" or "div" or "mod" or "shl" or "shr")
            return IsOrdinalArithmetic(value.Children[0]) && IsOrdinalArithmetic(value.Children[1]);

        string? type = ResolveExpressionType(value) ?? ResolveUnaryMemberType(value)?.ResolvedName;
        if (type is not null)
            return MapType(type) is "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long";
        return value.Kind == ExpressionParser.ExpressionKind.Call && value.Children.Count == 2 &&
            value.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } op &&
            op.Text.ToUpperInvariant() is "ROUND" or "TRUNC" &&
            _resolver is not null && _unitName is not null &&
            ResolveExpressionType(op) is null && ResolveUnaryMemberType(op) is null &&
            _currentTypeContext?.ResolveLocalFunction(op.Text, 1, valueOnly: false).Found != true &&
            !_resolver.HasVisibleValueDeclaration(op.Text, _unitName);
    }

    private string TranslateExpectedValue(ExpressionParser.ExpressionNode value, string? expectedType)
    {
        if (MapType(expectedType) == "HashSet<char>" &&
            value.Kind == ExpressionParser.ExpressionKind.Set)
            return TranslateCharacterSet(value);

        if (MapType(expectedType) == "char" && value.Kind == ExpressionParser.ExpressionKind.Literal &&
            TryTranslatePascalStringLiteral(value.Text, out string character, preferChar: true))
            return character;

        string mappedExpected = MapType(expectedType);
        if (value.Kind == ExpressionParser.ExpressionKind.Set && expectedType is not null &&
            value.Children.All(item => item.Kind != ExpressionParser.ExpressionKind.Binary || item.Text != ".."))
        {
            var array = System.Text.RegularExpressions.Regex.Match(expectedType.Trim(), @"^array\s+of\s+(.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (array.Success && mappedExpected.EndsWith("[]", StringComparison.Ordinal))
            {
                string elementType = array.Groups[1].Value.Trim();
                string items = string.Join(", ", value.Children.Select(item => TranslateExpectedValue(item, elementType)));
                return $"new {mappedExpected} {{ {items} }}";
            }
            if (mappedExpected.StartsWith("HashSet<", StringComparison.Ordinal) && mappedExpected.EndsWith('>'))
            {
                string elementType = mappedExpected[8..^1];
                if (_resolver?.IsZeroBasedEnumeration(elementType, _unitName) == true)
                {
                    string items = string.Join(", ", value.Children.Select(TranslateExpression));
                    return $"new {mappedExpected} {{ {items} }}";
                }
            }
        }
        if (expectedType is not null && _resolver is not null &&
            (mappedExpected is "bool" or "char" or "string" or "AnsiString" or "byte" or "sbyte"
                or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double"
                or "Extended" or "Variant" or "IntPtr" or "UIntPtr" ||
             _resolver.ResolveDeclarationKind(expectedType, _unitName) is "Class" or "Interface") &&
            TryTranslateBareUnaryFunction(value, out string invocation, out _))
            return invocation;

        string translated = TranslateExpression(value);
        if (mappedExpected == "float" &&
            value.Kind == ExpressionParser.ExpressionKind.Literal &&
            (translated.Contains('.') || translated.Contains('e', StringComparison.OrdinalIgnoreCase)) &&
            float.TryParse(translated, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return translated + "f";

        if (mappedExpected is "char" or "byte" or "sbyte" or "short" or "ushort" or
            "int" or "uint" or "long" or "ulong" or "float" or "double")
        {
            string? sourceType = ResolveExpressionType(value) ?? ResolveUnaryMemberType(value)?.ResolvedName;
            string mappedSource = MapType(sourceType);
            bool numericSource = mappedSource is "char" or "byte" or "sbyte" or "short" or "ushort" or
                "int" or "uint" or "long" or "ulong" or "float" or "double" ||
                value.Kind == ExpressionParser.ExpressionKind.Literal &&
                double.TryParse(translated, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
            if ((numericSource || IsOrdinalArithmetic(value)) && mappedSource != mappedExpected)
                return $"unchecked(({mappedExpected})({translated}))";
        }

        return translated;
    }

    internal static string DefaultParameterSuffix(IReadOnlyList<SemanticParameter> parameters,
        int index, Func<string, string> mapType)
    {
        string? suffix = null;
        for (int i = index; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            string? literal = parameter.DefaultValue?.Trim();
            if (string.IsNullOrEmpty(literal))
                return string.Empty;
            string type = mapType(parameter.Type);
            string? value = null;
            if (type == "bool" && bool.TryParse(literal, out bool boolean))
                value = boolean ? "true" : "false";
            else if (literal.Equals("nil", StringComparison.OrdinalIgnoreCase))
                value = "default";
            else if (type is "string" or "char" &&
                TryTranslatePascalStringLiteral(literal, out string text, type == "char") &&
                (type == "char" ? text.StartsWith("'") : text.StartsWith("\"")))
                value = text;
            else if (type is "int" or "uint" or "byte" or "sbyte" or "short" or "ushort" or "long" or "ulong")
            {
                if (long.TryParse(literal.Replace(" ", ""), System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out long number))
                    value = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if (literal.StartsWith('$') && literal.Length > 1 && literal[1..].All(Uri.IsHexDigit))
                    value = "0x" + literal[1..];
            }
            if (value is null)
                return string.Empty;
            if (i == index)
                suffix = " = " + value;
        }
        return suffix ?? string.Empty;
    }

    private string TranslateCondition(ExpressionParser.ExpressionNode? operand) =>
        operand is null ? string.Empty : TranslateValueOperand(operand, out _);

    private string TranslateValueOperand(ExpressionParser.ExpressionNode operand, out bool isBoolean)
    {
        if (TryTranslateBareUnaryFunction(operand, out string invocation, out isBoolean) ||
            TryTranslateProcedureValueInvocation(operand, out invocation, out isBoolean))
            return invocation;
        return TranslateExpression(operand);
    }

    private bool TryTranslateProcedureValueInvocation(
        ExpressionParser.ExpressionNode expression, out string invocation, out bool returnsBoolean)
    {
        invocation = string.Empty;
        returnsBoolean = false;
        if (_resolver is null || _unitName is null)
            return false;

        SemanticProcedureType? procedure = null;
        if (expression.Kind == ExpressionParser.ExpressionKind.Identifier)
            procedure = _resolver.FindUnitProcedureVariable(expression.Text, _unitName);
        else if (expression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
            expression.Children.Count == 2)
        {
            ExpressionParser.ExpressionNode instance = expression.Children[0];
            string? receiver = ResolveExpressionType(instance) ?? ResolveUnaryMemberType(instance)?.ResolvedName;
            if (receiver is not null &&
                _resolver.FindMember(receiver, expression.Children[1].Text, _unitName, null) is { } member)
                procedure = _resolver.ResolveProcedureType(member.Member.Type, member.UnitName);
        }

        if (procedure is null || procedure.Parameters.Count != 0 || procedure.IsProcedure)
            return false;

        invocation = TranslateExpression(expression) + "()";
        returnsBoolean = MapType(procedure.ReturnType) == "bool";
        return true;
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

        var operandExpression = expression.Children[0];
        if (expression.Text.Equals("not", StringComparison.OrdinalIgnoreCase) &&
            TryTranslateBareUnaryFunction(operandExpression, out string call, out bool returnsBoolean))
            return (returnsBoolean ? "!" : "~") + call;

        string operand = TranslateExpression(operandExpression);

        string op =
            expression.Text.ToLowerInvariant() switch
            {
                "not" => IsBooleanUnaryOperand(expression.Children[0]) ? "!" : "~",
                "+" => "+",
                "-" => "-",
                _ => expression.Text
            };

        return $"{op}{operand}";
    }

    private bool TryTranslateBareUnaryFunction(
        ExpressionParser.ExpressionNode expression, out string call, out bool returnsBoolean)
    {
        call = string.Empty;
        returnsBoolean = false;
        var function = ResolveUnaryFunctionTarget(expression, 0);
        if (function is not { } resolved || !resolved.Type.IsResolved || resolved.Type.IsPointer)
            return false;

        returnsBoolean = CSharpTypeMapper.Map(resolved.Type.ResolvedName) == "bool";
        string prefix = expression.Kind == ExpressionParser.ExpressionKind.MemberAccess
            ? TranslateValueOperand(expression.Children[0], out _) + "."
            : "";
        call = prefix + resolved.Name + "()";
        return true;
    }

    private (string Name, TypeResolution Type)? ResolveUnaryFunctionTarget(
        ExpressionParser.ExpressionNode target, int argumentCount)
    {
        if (_resolver is null)
            return null;

        if (target.Kind == ExpressionParser.ExpressionKind.Identifier)
        {
            if (_currentTypeContext is null)
                return null;

            var (found, local) = _currentTypeContext.ResolveLocalFunction(target.Text, argumentCount);
            if (found)
                return local is null ? null : (local.Name, _resolver.Resolve(local.ReturnType, _unitName));

            if (ResolveExpressionType(target) is not null || ResolveUnaryMemberType(target) is not null)
                return null;
            if (_currentTypeContext.OwnerClass is { } owner &&
                _resolver.ResolveFunctionMember(owner, target.Text, _unitName, argumentCount) is { } method)
                return method;
            return _resolver.ResolveUnitFunction(target.Text, _unitName, argumentCount);
        }

        if (target.Kind != ExpressionParser.ExpressionKind.MemberAccess || target.Children.Count != 2)
            return null;

        var receiver = target.Children[0];
        string? receiverName = ResolveExpressionType(receiver);
        string? unitName = _unitName;
        if (receiverName is null &&
            ResolveUnaryMemberType(receiver) is { IsResolved: true, IsPointer: false } member)
        {
            receiverName = member.ResolvedName;
            unitName = member.DeclaringUnitName ?? _unitName;
        }
        return receiverName is null
            ? null
            : _resolver.ResolveFunctionMember(receiverName, target.Children[1].Text, unitName, argumentCount);
    }

    private string TranslateDefaultArgument(SemanticParameter parameter)
    {
        string literal = parameter.DefaultValue?.Trim() ??
            throw new InvalidOperationException($"Missing default for {parameter.Name}.");
        string mapped = MapType(parameter.Type);

        if (literal == "[]" && mapped.StartsWith("HashSet<", StringComparison.Ordinal))
            return $"new {mapped}()";
        if (literal.Equals("nil", StringComparison.OrdinalIgnoreCase))
            return "default";
        if (mapped == "bool" && bool.TryParse(literal, out bool boolean))
            return boolean ? "true" : "false";
        if (mapped is "string" or "char" &&
            TryTranslatePascalStringLiteral(literal, out string text, mapped == "char"))
            return text;
        if (literal.StartsWith('$') && literal.Length > 1 && literal[1..].All(Uri.IsHexDigit))
            return "0x" + literal[1..];
        return literal;
    }

    private static bool IsAssignedCall(ExpressionParser.ExpressionNode expression) =>
        expression.Kind == ExpressionParser.ExpressionKind.Call &&
        expression.Children.Count == 2 &&
        expression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children[0].Text.Equals("Assigned", StringComparison.OrdinalIgnoreCase);

    private bool IsBooleanUnaryOperand(ExpressionParser.ExpressionNode operand)
    {
        if (operand.Kind == ExpressionParser.ExpressionKind.TypeTest || IsAssignedCall(operand))
            return true;

        if (operand.Kind == ExpressionParser.ExpressionKind.Unary && operand.Children.Count == 1 &&
            operand.Text.Equals("not", StringComparison.OrdinalIgnoreCase))
            return IsBooleanUnaryOperand(operand.Children[0]);

        if (operand.Kind == ExpressionParser.ExpressionKind.Binary && operand.Children.Count == 2)
        {
            return operand.Text.ToLowerInvariant() switch
            {
                "=" or "<>" or "<" or "<=" or ">" or ">=" or "in" => true,
                "and" or "or" or "xor" =>
                    IsBooleanUnaryOperand(operand.Children[0]) && IsBooleanUnaryOperand(operand.Children[1]),
                _ => false
            };
        }

        if (operand.Kind == ExpressionParser.ExpressionKind.Identifier &&
            (operand.Text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             operand.Text.Equals("false", StringComparison.OrdinalIgnoreCase)))
            return true;

        string? localType = ResolveExpressionType(operand);
        if (localType is not null)
            return IsBooleanType(localType);

        if (operand.Kind == ExpressionParser.ExpressionKind.Call && operand.Children.Count > 0 &&
            ResolveUnaryFunctionTarget(operand.Children[0], operand.Children.Count - 1) is { } function)
            return function.Type.IsResolved && !function.Type.IsPointer &&
                CSharpTypeMapper.Map(function.Type.ResolvedName) == "bool";

        if (operand.Kind == ExpressionParser.ExpressionKind.Call && operand.Children.Count == 2)
        {
            var target = operand.Children[0];
            if (target.Kind == ExpressionParser.ExpressionKind.Identifier &&
                target.Text.ToUpperInvariant() is ("VARISNUMERIC" or "VARISEMPTY" or "VARISNULL") &&
                _currentTypeContext?.ResolveLocalFunction(target.Text, 1).Found != true &&
                ResolveExpressionType(target) is null &&
                (_unitName is null || _resolver?.HasVisibleNonTypeDeclaration(target.Text, _unitName) != true))
                return true;
        }

        if (TryTranslateBareUnaryFunction(operand, out _, out bool returnsBoolean))
            return returnsBoolean;

        TypeResolution? member = ResolveUnaryMemberType(operand);
        return member is { IsResolved: true, IsPointer: false } &&
            CSharpTypeMapper.Map(member.ResolvedName) == "bool";
    }

    private TypeResolution? ResolveUnaryMemberType(ExpressionParser.ExpressionNode expression)
    {
        if (_resolver is null)
            return null;

        if (expression.Kind == ExpressionParser.ExpressionKind.Identifier)
        {
            if (_currentTypeContext?.OwnerClass is { } owner &&
                _resolver.ResolveDataMemberType(owner, expression.Text, _unitName) is { } memberType)
                return memberType;
            return _resolver.FindUnitVariableType(expression.Text, _unitName);
        }

        if (expression.Kind != ExpressionParser.ExpressionKind.MemberAccess ||
            expression.Children.Count != 2)
            return null;

        var receiver = expression.Children[0];
        string? local = ResolveExpressionType(receiver);
        if (!string.IsNullOrWhiteSpace(local))
            return _resolver.ResolveDataMemberType(local, expression.Children[1].Text, _unitName);

        TypeResolution? receiverType = ResolveUnaryMemberType(receiver);
        return receiverType is { IsResolved: true, IsPointer: false }
            ? _resolver.ResolveDataMemberType(receiverType.ResolvedName, expression.Children[1].Text,
                receiverType.DeclaringUnitName ?? _unitName)
            : null;
    }

    private string? ResolveExpressionType(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null ||
            _currentTypeContext is null)
        {
            return null;
        }

        if (expression.Kind == ExpressionParser.ExpressionKind.Call && expression.Children.Count > 0 &&
            ResolveUnaryFunctionTarget(expression.Children[0], expression.Children.Count - 1) is { } routine &&
            routine.Type.IsResolved && !routine.Type.IsPointer)
            return routine.Type.ResolvedName;

        if (expression.Kind == ExpressionParser.ExpressionKind.IndexAccess)
            return ResolveArrayElement(expression);

        if (expression.Kind ==
            ExpressionParser.ExpressionKind.Identifier)
        {
            if (expression.Text.Equals("Self", StringComparison.OrdinalIgnoreCase))
                return _currentTypeContext.OwnerClass;
            return _currentTypeContext.ResolveIdentifier(expression.Text);
        }

        return null;
    }

    private string TranslateMemberAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        var instance = expression.Children[0];
        string target = TranslateValueOperand(instance, out _);
        string member = expression.Children[1].Text;
        string? receiver = ResolveExpressionType(instance);
        string? receiverUnit = _unitName;
        if (receiver is null && ResolveUnaryMemberType(instance) is { IsResolved: true, IsPointer: false } field)
        {
            receiver = field.ResolvedName;
            receiverUnit = field.DeclaringUnitName ?? _unitName;
        }
        if (receiver is not null && _resolver is not null)
        {
            TypeResolution receiverResolution = _resolver.Resolve(receiver, receiverUnit);
            if (receiverResolution.IsPointer && MapType(receiver) != "Td2dRenderTarget" && receiverResolution.DeclaringUnitName is not null &&
                _resolver.FindRecordField(receiver, member, receiverUnit) is { } pointerField)
            {
                target = $"global::FireURQ.Runtime.DelphiSystem.Dereference<{receiverResolution.ResolvedName}>({target})";
                member = pointerField.Name;
            }
            else if (_resolver.FindMember(receiver, member, receiverUnit, null) is { } binding)
                member = binding.Member.Name;
            else if (_resolver.FindRecordField(receiver, member, receiverUnit) is { } recordField)
                member = recordField.Name;
        }

        // A read of LongRec exposes the low or high Word of a 32-bit value.
        // Keep address-taking and writes on the separate lvalue path.
        if (!_translatingAssignmentTarget && _resolver is not null && _unitName is not null &&
            expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Call } recordCast &&
            recordCast.Children.Count == 2 &&
            recordCast.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
            recordCast.Children[0].Text.Equals("LongRec", StringComparison.OrdinalIgnoreCase) &&
            !_resolver.HasVisibleValueDeclaration("LongRec", _unitName) &&
            ResolveExpressionType(recordCast.Children[0]) is null &&
            ResolveExpressionType(recordCast.Children[1]) is string packedType &&
            MapType(packedType) is "int" or "uint" or "IntPtr" or "UIntPtr")
        {
            string packed = TranslateExpression(recordCast.Children[1]);
            if (member.Equals("Lo", StringComparison.OrdinalIgnoreCase))
                return $"unchecked((ushort)(uint)({packed}))";
            if (member.Equals("Hi", StringComparison.OrdinalIgnoreCase))
                return $"unchecked((ushort)((uint)({packed}) >> 16))";
        }

        if (string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
            member.Equals("VType", StringComparison.OrdinalIgnoreCase) &&
            expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Call } cast &&
            cast.Children.Count == 2 &&
            cast.Children[0].Text.Equals("TVarData", StringComparison.OrdinalIgnoreCase))
            return $"({TranslateExpression(cast.Children[1])}).TypeCode";

        if (string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
            expression.Children[0].Text.Equals("aReader", StringComparison.OrdinalIgnoreCase) &&
            member.Equals("ReadLongint", StringComparison.OrdinalIgnoreCase))
            member = "ReadLongint";

        return $"{target}.{member}";
    }

    private bool _translatingAssignmentTarget;

    private SemanticMember? ResolveIndexedProperty(ExpressionParser.ExpressionNode index)
    {
        if (_resolver is null || index.Kind != ExpressionParser.ExpressionKind.IndexAccess ||
            index.Children.Count < 2)
            return null;
        var target = index.Children[0];
        string? owner;
        string? context = _unitName;
        string name;
        if (target.Kind == ExpressionParser.ExpressionKind.MemberAccess && target.Children.Count == 2)
        {
            var receiver = target.Children[0];
            owner = ResolveExpressionType(receiver);
            if (owner is null && ResolveUnaryMemberType(receiver) is { } field)
            {
                owner = field.ResolvedName;
                context = field.DeclaringUnitName ?? _unitName;
            }
            name = target.Children[1].Text;
        }
        else if (target.Kind == ExpressionParser.ExpressionKind.Identifier &&
                 ResolveExpressionType(target) is null)
        {
            owner = _currentTypeContext?.OwnerClass;
            name = target.Text;
        }
        else return null;

        return owner is not null &&
            _resolver.FindMember(owner, name, context, null) is { } found &&
            found.Member.Kind == SemanticMemberKind.Property &&
            found.Member.Parameters.Count == index.Children.Count - 1
                ? found.Member : null;
    }

    private bool TryTranslateIndexedPropertyWrite(
        ExpressionParser.ExpressionNode target,
        ExpressionParser.ExpressionNode value,
        out string translated)
    {
        translated = string.Empty;

        if (_resolver is null ||
            target.Kind != ExpressionParser.ExpressionKind.IndexAccess ||
            target.Children.Count < 2)
            return false;

        var access = target.Children[0];
        if (ResolveIndexedProperty(target) is not { } property || string.IsNullOrWhiteSpace(property.WriteAccessor))
            return false;
        string receiver = access.Kind == ExpressionParser.ExpressionKind.MemberAccess
            ? TranslateExpression(access.Children[0]) + "." : "";


        var arguments = target.Children
            .Skip(1)
            .Select(TranslateExpression)
            .ToList();

        arguments.Add(TranslateExpression(value));

        translated =
            $"{receiver}{property.WriteAccessor}(" +
            string.Join(", ", arguments) + ");";

        return true;
    }

    private bool TryTranslateIndexedPropertyRead(
        ExpressionParser.ExpressionNode expression,
        out string translated)
    {
        translated = string.Empty;

        if (_translatingAssignmentTarget ||
            _resolver is null ||
            expression.Children.Count < 2)
            return false;

        var access = expression.Children[0];
        if (ResolveIndexedProperty(expression) is not { } property || string.IsNullOrWhiteSpace(property.ReadAccessor))
            return false;
        string receiver = access.Kind == ExpressionParser.ExpressionKind.MemberAccess
            ? TranslateExpression(access.Children[0]) + "." : "";


        string arguments = string.Join(", ",
            expression.Children.Skip(1).Select(TranslateExpression));

        translated =
            $"{receiver}{property.ReadAccessor}({arguments})";

        return true;
    }

    private string? PointerArrayElement(ExpressionParser.ExpressionNode array)
    {
        if (_resolver is null) return null;
        string? local = ResolveExpressionType(array);
        var pointer = local is null ? ResolveUnaryMemberType(array) : _resolver.Resolve(local, _unitName);
        if (pointer is not { IsResolved: true, PointerDepth: 1 }) return null;
        string definition = pointer.ResolvedName;
        string? context = pointer.DeclaringUnitName ?? _unitName;
        if (_resolver.TryGetCompositeDefinition(definition, context, out string composite, out _))
            definition = composite;
        var match = System.Text.RegularExpressions.Regex.Match(definition,
            @"^array\s*\[\s*0\s*\.\.\s*[^,\]]+\]\s*of\s+(\w+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        string element = match.Groups[1].Value;
        string mapped = CSharpTypeMapper.Map(element, _resolver, context);
        return mapped is "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double"
            || _resolver.FixedScalarRecordSize(element, context) > 0 ? mapped : null;
    }

    private string? ResolveArrayElement(ExpressionParser.ExpressionNode index)
    {
        if (_resolver is null ||
            index.Kind != ExpressionParser.ExpressionKind.IndexAccess ||
            index.Children.Count < 2)
            return null;

        if (ResolveIndexedProperty(index) is { } property)
            return property.Type;

        ExpressionParser.ExpressionNode array = index.Children[0];
        string? arrayType =
            ResolveExpressionType(array) ??
            ResolveUnaryMemberType(array)?.ResolvedName;
        if (arrayType is null)
            return null;

        if (index.Children.Count == 2 && MapType(arrayType) == "string")
            return "Char";

        TypeResolution resolved = _resolver.Resolve(arrayType, _unitName);
        if (!resolved.IsResolved || resolved.IsPointer)
            return null;

        string definition = resolved.ResolvedName;
        if (_resolver.TryGetCompositeDefinition(
                definition,
                resolved.DeclaringUnitName ?? _unitName,
                out string composite,
                out _))
            definition = composite;

        var match = System.Text.RegularExpressions.Regex.Match(
            definition,
            @"^array\s*(?:\[[^\]]*\]\s*)?of\s+(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private bool IsManagedString(ExpressionParser.ExpressionNode expression)
    {
        string? type = ResolveExpressionType(expression) ?? ResolveUnaryMemberType(expression)?.ResolvedName;
        return type is not null && MapType(type) == "string";
    }

    private string TranslateIndexAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count < 2)
            return expression.Text;

        if (TryTranslateIndexedPropertyRead(expression, out string translated))
            return translated;

        string target = TranslateExpression(expression.Children[0]);
        if (expression.Children.Count == 2 && PointerArrayElement(expression.Children[0]) is string element)
            return $"global::FireURQ.Runtime.DelphiSystem.ElementAt<{element}>({target}, checked((nint)({TranslateValueOperand(expression.Children[1], out _)})))";
        if (expression.Children.Count == 2 && IsManagedString(expression.Children[0]))
            return $"{target}[({TranslateValueOperand(expression.Children[1], out _)}) - 1]";

        string[] indices = expression.Children.Skip(1).Select(TranslateExpression).ToArray();
        if (_resolver is not null)
        {
            var field = ResolveUnaryMemberType(expression.Children[0]);
            string? arrayType = ResolveExpressionType(expression.Children[0]) ?? field?.ResolvedName;
            string? context = field?.DeclaringUnitName ?? _unitName;
            if (arrayType is not null && _resolver.TryGetCompositeDefinition(arrayType, context,
                out string definition, out string? declarationUnit))
            {
                arrayType = definition;
                context = declarationUnit;
            }
            var bounds = System.Text.RegularExpressions.Regex.Match(arrayType ?? string.Empty,
                @"^array\s*\[([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (bounds.Success)
            {
                string[] domains = bounds.Groups[1].Value.Split(',');
                if (domains.Length == indices.Length)
                    for (int i = 0; i < domains.Length; i++)
                        if (_resolver.IsZeroBasedEnumeration(domains[i].Trim(), context))
                            indices[i] = $"(int)({indices[i]})";
            }
        }
        return $"{target}[{string.Join(", ", indices)}]";
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
            }

            if (target.Text.Equals("Delete", StringComparison.OrdinalIgnoreCase) &&
                expression.Children.Count == 4 && value.Kind == ExpressionParser.ExpressionKind.Identifier &&
                ResolveExpressionType(value) is string stringType && MapType(stringType) == "string")
            {
                string storage = TranslateExpression(value);
                string index = TranslateValueOperand(expression.Children[2], out _);
                string deleteCount = TranslateValueOperand(expression.Children[3], out _);
                translated = $"global::FireURQ.Runtime.DelphiStorage.Delete(ref {storage}, {index}, {deleteCount})";
                return true;
            }

            if (target.Text.Equals("SetLength", StringComparison.OrdinalIgnoreCase) &&
                expression.Children.Count == 3 &&
                value.Kind is ExpressionParser.ExpressionKind.Identifier or ExpressionParser.ExpressionKind.MemberAccess)
            {
                string? local = ResolveExpressionType(value);
                var type = local is null ? ResolveUnaryMemberType(value) : _resolver.Resolve(local, _unitName);
                if (type is { IsResolved: true, IsPointer: false })
                {
                    string definition = type.ResolvedName;
                    if (_resolver.TryGetCompositeDefinition(definition, type.DeclaringUnitName ?? _unitName,
                        out string composite, out _))
                        definition = composite;

                    if (MapType(type.ResolvedName) == "string" ||
                        definition.StartsWith("array of ", StringComparison.OrdinalIgnoreCase))
                    {
                        string storage = TranslateExpression(value);
                        string length = TranslateValueOperand(expression.Children[2], out _);
                        translated = $"global::FireURQ.Runtime.DelphiStorage.SetLength(ref {storage}, checked((int)({length})))";
                        return true;
                    }
                }
            }

            if (target.Text.Equals("Length", StringComparison.OrdinalIgnoreCase) &&
                expression.Children.Count == 2 &&
                ResolveExpressionType(value) is string valueType)
            {
                string mapped = MapType(valueType);
                if (IsStandardAnsiStringValue(value))
                {
                    translated = $"{TranslateExpression(value)}.Length";
                    return true;
                }

                // A Delphi nil dynamic array or empty string has length zero.
                bool dynamicArray = mapped.EndsWith("[]", StringComparison.Ordinal) &&
                    _resolver.Resolve(valueType, _unitName).ResolvedName
                        .StartsWith("array of ", StringComparison.OrdinalIgnoreCase);
                if (mapped == "string" || dynamicArray)
                {
                    translated = $"(({TranslateExpression(value)})?.Length ?? 0)";
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
        if (!resolution.IsResolved || resolution.IsPointer)
            return false;
        if (resolution.Chain.Count == 0 && resolution.DeclaringUnitName is null &&
            !target.Text.Equals("SmallInt", StringComparison.OrdinalIgnoreCase) &&
            !target.Text.Equals("ShortInt", StringComparison.OrdinalIgnoreCase))
            return false;

        string type = MapType(target.Text);
        string value = TranslateExpression(expression.Children[1]);
        translated = $"(({type})({value}))";
        return true;
    }
    private bool TryTranslateSimpleXmlBinaryCall(
        ExpressionParser.ExpressionNode expression,
        out string translated)
    {
        translated = string.Empty;
        if (expression.Children.Count < 2 ||
            expression.Children[0] is not { Kind: ExpressionParser.ExpressionKind.MemberAccess } target ||
            target.Children.Count != 2)
            return false;

        string receiver = target.Children[0].Text.ToUpperInvariant();
        string method = target.Children[1].Text.ToUpperInvariant();
        var argument = expression.Children[1];
        if (expression.Children.Count == 2 &&
            ((receiver == "AREADER" && method == "READVARIANT") ||
             (receiver == "AWRITER" && method == "WRITEVARIANT")))
        {
            if (argument.Kind == ExpressionParser.ExpressionKind.Call &&
                argument.Children.Count == 2 &&
                argument.Children[0].Text.Equals("TVarData", StringComparison.OrdinalIgnoreCase))
                argument = argument.Children[1];

            if (receiver == "AREADER")
                translated = $"aReader.ReadVariant(ref {TranslateExpression(argument)})";
            else
                translated = $"aWriter.WriteVariant({(argument.Text.Equals("EmptyVar", StringComparison.OrdinalIgnoreCase) ? "Variant.Unassigned" : TranslateExpression(argument))})";

            return true;
        }

        if (expression.Children.Count == 3 && receiver == "ASTREAM" && method == "READBUFFER" &&
            argument.Kind == ExpressionParser.ExpressionKind.IndexAccess &&
            argument.Children.Count == 2 &&
            argument.Children[0].Text.Equals("aBinarySign", StringComparison.OrdinalIgnoreCase))
        {
            translated = "aBinarySign = TStmXmlReader.ReadSignature(aStream)";
            return true;
        }

        if (expression.Children.Count != 3 ||
            !((receiver == "AREADER" && method == "READ") ||
              (receiver == "AWRITER" && method == "WRITE")))
            return false;

        if (argument.Text.Equals("aNodeType", StringComparison.OrdinalIgnoreCase))
        {
            translated = receiver == "AREADER"
                ? "aNodeType = aReader.ReadNodeType()"
                : "aWriter.WriteNodeType(aNodeType)";
            return true;
        }

        if (argument.Kind == ExpressionParser.ExpressionKind.IndexAccess &&
            argument.Children.Count == 2 &&
            argument.Children[0] is { Kind: ExpressionParser.ExpressionKind.IndexAccess } bucket &&
            bucket.Children.Count == 2 &&
            bucket.Children[0].Text.Equals("FHashTable", StringComparison.OrdinalIgnoreCase))
        {
            translated = receiver == "AREADER"
                ? $"aReader.ReadNameIndexes({TranslateExpression(bucket)})"
                : $"aWriter.WriteNameIndexes({TranslateExpression(bucket)})";
            return true;
        }

        return false;
    }

private string TranslateCall(
    ExpressionParser.ExpressionNode expression)
{
    if (_unitName is not null && _resolver is not null && expression.Children.Count == 13 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } createWindow &&
        createWindow.Text.Equals("CreateWindowEx", StringComparison.OrdinalIgnoreCase) &&
        !_resolver.HasVisibleValueDeclaration(createWindow.Text, _unitName) &&
        expression.Children[3] is { Kind: ExpressionParser.ExpressionKind.Call } title &&
        title.Children.Count == 2 && title.Children[0].Text.Equals("PChar", StringComparison.OrdinalIgnoreCase) &&
        IsManagedString(title.Children[1]))
    {
        var windowArguments = expression.Children.Skip(1).Select(TranslateExpression).ToArray();
        windowArguments[2] = TranslateExpression(title.Children[1]);
        for (int i = 8; i < windowArguments.Length; i++)
            if (IsNilLiteral(expression.Children[i + 1])) windowArguments[i] = "global::System.IntPtr.Zero";
        return $"global::FireURQ.Runtime.DelphiWindows.CreateWindowEx({string.Join(", ", windowArguments)})";
    }

    // The Delphi PChar cast borrows the command string for this call only.
    if (expression.Children.Count == 5 && _unitName is not null && _resolver is not null &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } mci &&
        mci.Text.Equals("mciSendString", StringComparison.OrdinalIgnoreCase) &&
        !_resolver.HasVisibleValueDeclaration(mci.Text, _unitName) &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Call } mciCast &&
        mciCast.Children.Count == 2 &&
        mciCast.Children[0].Text.Equals("PChar", StringComparison.OrdinalIgnoreCase) &&
        MapType(ResolveExpressionType(mciCast.Children[1])) == "string")
    {
        return "global::FireURQ.Runtime.DelphiWindows.mciSendString(" +
            TranslateExpression(mciCast.Children[1]) + ", " +
            string.Join(", ", expression.Children.Skip(2).Select(TranslateExpression)) + ")";
    }

    // This D2D pointer is represented by a managed object because its fields own COM references.
    if (_unitName?.Equals("d2dCore", StringComparison.OrdinalIgnoreCase) == true &&
        expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } renderOperation)
    {
        var operand = expression.Children[1];
        if (renderOperation.Text.Equals("Pd2dRenderTarget", StringComparison.OrdinalIgnoreCase) &&
            operand.Kind == ExpressionParser.ExpressionKind.IndexAccess && operand.Children.Count == 2 &&
            operand.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } items &&
            items.Children.Count == 2 && items.Children[0].Text.Equals("f_Targets", StringComparison.OrdinalIgnoreCase) &&
            items.Children[1].Text.Equals("Items", StringComparison.OrdinalIgnoreCase))
            return $"f_Targets[{TranslateExpression(operand.Children[1])}]";

        if (renderOperation.Text.Equals("Dispose", StringComparison.OrdinalIgnoreCase) &&
            (MapType(ResolveExpressionType(operand)) == "Td2dRenderTarget" ||
             operand.Kind == ExpressionParser.ExpressionKind.Call && operand.Children.Count == 2 &&
             operand.Children[0].Text.Equals("Pd2dRenderTarget", StringComparison.OrdinalIgnoreCase)))
            return $"ReleaseTargetResources({TranslateExpression(operand)})";
        if (renderOperation.Text.Equals("New", StringComparison.OrdinalIgnoreCase) &&
            MapType(ResolveExpressionType(operand)) == "Td2dRenderTarget")
            return $"{TranslateExpression(operand)} = new Td2dRenderTarget()";
    }

    if (expression.Children.Count > 1 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } d3dxFactory &&
        d3dxFactory.Text.ToUpperInvariant() is ("D3DXCREATETEXTURE" or "D3DXCREATETEXTUREFROMFILEINMEMORYEX" or "D3DXCREATEFONTINDIRECT") &&
        (_resolver is null || _unitName is null ||
         !_resolver.HasVisibleValueDeclaration(d3dxFactory.Text, _unitName)))
    {
        string factory = d3dxFactory.Text.ToUpperInvariant();
        string[] factoryArguments = expression.Children.Skip(1)
            .Select(TranslateExpression).ToArray();
        if (factory == "D3DXCREATETEXTURE" && factoryArguments.Length == 8)
        {
            for (int index = 1; index <= 4; index++)
                factoryArguments[index] = $"checked((uint)({factoryArguments[index]}))";
            factoryArguments[7] = "out " + factoryArguments[7];
            return $"D3DXCreateTexture({string.Join(", ", factoryArguments)})";
        }
        if (factory == "D3DXCREATETEXTUREFROMFILEINMEMORYEX" &&
            factoryArguments.Length == 15)
        {
            foreach (int index in new[] { 2, 3, 4, 5, 6, 9, 10, 11 })
                factoryArguments[index] = $"unchecked((uint)({factoryArguments[index]}))";
            if (expression.Children[13] is { Kind: ExpressionParser.ExpressionKind.AddressOf } infoAddress &&
                infoAddress.Children.Count == 1)
                factoryArguments[12] = TranslateExpression(infoAddress.Children[0]);
            if (IsNilLiteral(expression.Children[14]))
                factoryArguments[13] = "global::System.IntPtr.Zero";
            factoryArguments[12] = "out " + factoryArguments[12];
            factoryArguments[14] = "out " + factoryArguments[14];
            return "global::FireURQ.Runtime.D3DX8.CreateTextureFromFileInMemoryEx(" +
                string.Join(", ", factoryArguments) + ")";
        }
        if (factory == "D3DXCREATEFONTINDIRECT" && factoryArguments.Length == 3)
        {
            factoryArguments[1] = "in " + factoryArguments[1];
            factoryArguments[2] = "out " + factoryArguments[2];
            return $"D3DXCreateFontIndirect({string.Join(", ", factoryArguments)})";
        }
    }

    if (expression.Children.Count >= 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } d3dxMatrix &&
        d3dxMatrix.Text.ToUpperInvariant() is ("D3DXMATRIXIDENTITY" or "D3DXMATRIXTRANSLATION" or "D3DXMATRIXSCALING" or
            "D3DXMATRIXROTATIONZ" or "D3DXMATRIXMULTIPLY" or "D3DXMATRIXORTHOOFFCENTERLH") &&
        (_resolver is null || _unitName is null ||
         !_resolver.HasVisibleValueDeclaration(d3dxMatrix.Text, _unitName)))
    {
        string operation = d3dxMatrix.Text.ToUpperInvariant();
        string[] matrixArguments = expression.Children.Skip(1)
            .Select(TranslateExpression).ToArray();
        if (operation == "D3DXMATRIXIDENTITY" && matrixArguments.Length == 1)
            return $"D3DXMatrixIdentity(ref {matrixArguments[0]})";
        if (operation is "D3DXMATRIXTRANSLATION" or "D3DXMATRIXSCALING" &&
            matrixArguments.Length == 4)
            return $"{d3dxMatrix.Text}(ref {matrixArguments[0]}, " +
                string.Join(", ", matrixArguments.Skip(1).Select(value => $"(float)({value})")) + ")";
        if (operation == "D3DXMATRIXROTATIONZ" && matrixArguments.Length == 2)
            return $"D3DXMatrixRotationZ(ref {matrixArguments[0]}, (float)({matrixArguments[1]}))";
        if (operation == "D3DXMATRIXMULTIPLY" && matrixArguments.Length == 3)
            return $"D3DXMatrixMultiply(ref {matrixArguments[0]}, in {matrixArguments[1]}, in {matrixArguments[2]})";
        if (operation == "D3DXMATRIXORTHOOFFCENTERLH" && matrixArguments.Length == 7)
            return $"D3DXMatrixOrthoOffCenterLH(ref {matrixArguments[0]}, " +
                string.Join(", ", matrixArguments.Skip(1).Select(value => $"(float)({value})")) + ")";
    }

    if (expression.Children.Count == 3 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } setLength &&
        setLength.Text.Equals("SetLength", StringComparison.OrdinalIgnoreCase) &&
        (_resolver is null || _unitName is null ||
         !_resolver.HasVisibleValueDeclaration(setLength.Text, _unitName)))
    {
        ExpressionParser.ExpressionNode value = expression.Children[1];
        string translatedValue = TranslateExpression(value);
        string valueType = MapType(ResolveExpressionType(value));
        string length = $"checked((int)({TranslateExpression(expression.Children[2])}))";
        if (valueType == "string")
            return $"{translatedValue} = global::FireURQ.Runtime.DelphiText.SetStringLength({translatedValue}, {length})";
        if (valueType == "AnsiString")
            return $"{translatedValue} = {translatedValue}.WithLength({length})";
        if (valueType.EndsWith("[]", StringComparison.Ordinal))
            return $"global::System.Array.Resize(ref {translatedValue}, {length})";

        // Delphi SetLength's remaining legal target is a dynamic array. Let C#
        // infer its element type when an imported alias obscures it here.
        return $"global::System.Array.Resize(ref {translatedValue}, {length})";
    }

    if (expression.Children.Count == 4 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } memoryOperation &&
        memoryOperation.Text.ToUpperInvariant() is "FILLCHAR" or "MOVE" &&
        (_resolver is null || _unitName is null ||
         !_resolver.HasVisibleValueDeclaration(memoryOperation.Text, _unitName)))
    {
        bool IsNativeDereference(ExpressionParser.ExpressionNode node) =>
            node.Kind == ExpressionParser.ExpressionKind.Dereference && node.Children.Count == 1 &&
            ResolveExpressionType(node.Children[0]) is string declared && _resolver?.Resolve(declared, _unitName).IsPointer == true;
        if (memoryOperation.Text.Equals("Move", StringComparison.OrdinalIgnoreCase) &&
            IsNativeDereference(expression.Children[1]) && IsNativeDereference(expression.Children[2]))
            return $"global::FireURQ.Runtime.DelphiSystem.MoveNative({TranslateExpression(expression.Children[1].Children[0])}, {TranslateExpression(expression.Children[2].Children[0])}, checked((int)({TranslateExpression(expression.Children[3])})))";
        if (memoryOperation.Text.Equals("FillChar", StringComparison.OrdinalIgnoreCase) && IsNativeDereference(expression.Children[1]))
            return $"global::FireURQ.Runtime.DelphiSystem.FillNative({TranslateExpression(expression.Children[1].Children[0])}, checked((int)({TranslateExpression(expression.Children[2])})), unchecked((byte)({TranslateExpression(expression.Children[3])})))";
        if (memoryOperation.Text.Equals("FillChar", StringComparison.OrdinalIgnoreCase))
        {
            ExpressionParser.ExpressionNode destination = expression.Children[1];
            string memoryDestinationType = MapType(ResolveExpressionType(destination));
            string destinationArgument = memoryDestinationType.EndsWith("[]", StringComparison.Ordinal)
                ? TranslateExpression(destination)
                : "ref " + TranslateExpression(destination);
            return "global::FireURQ.Runtime.DelphiSystem.FillChar(" +
                $"{destinationArgument}, checked((int)({TranslateExpression(expression.Children[2])})), " +
                $"checked((int)({TranslateExpression(expression.Children[3])})))";
        }

        ExpressionParser.ExpressionNode source = expression.Children[1];
        ExpressionParser.ExpressionNode destinationMove = expression.Children[2];
        string sourceType = MapType(ResolveExpressionType(source) ?? ResolveUnaryMemberType(source)?.ResolvedName);
        if (sourceType.EndsWith("[]", StringComparison.Ordinal) &&
            destinationMove.Kind == ExpressionParser.ExpressionKind.IndexAccess && destinationMove.Children.Count == 2 &&
            PointerArrayElement(destinationMove.Children[0]) is string nativeElement &&
            sourceType == nativeElement + "[]")
            return $"global::FireURQ.Runtime.DelphiSystem.CopyToNative({TranslateExpression(source)}, ref {TranslateExpression(destinationMove)}, checked((int)({TranslateExpression(expression.Children[3])})))";
        string destinationTypeMove = MapType(ResolveExpressionType(destinationMove));
        string sourceArgument = sourceType.EndsWith("[]", StringComparison.Ordinal)
            ? TranslateExpression(source)
            : "ref " + TranslateExpression(source);
        string destinationArgumentMove = destinationTypeMove.EndsWith("[]", StringComparison.Ordinal)
            ? TranslateExpression(destinationMove)
            : "ref " + TranslateExpression(destinationMove);
        return "global::FireURQ.Runtime.DelphiSystem.Move(" +
            $"{sourceArgument}, {destinationArgumentMove}, " +
            $"checked((int)({TranslateExpression(expression.Children[3])})))";
    }

    if (_resolver is not null && _unitName is not null &&
        expression.Children.Count == 3 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } getMem &&
        getMem.Text.Equals("GetMem", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Identifier } allocationDestination &&
        ResolveExpressionType(allocationDestination) is string destinationType &&
        _resolver.Resolve(destinationType, _unitName).IsPointer)
    {
        return $"{TranslateExpression(allocationDestination)} = global::FireURQ.Runtime.DelphiSystem.GetMemory(checked((int)({TranslateExpression(expression.Children[2])})))";
    }

    if (expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } dispose &&
        dispose.Text.Equals("Dispose", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Call } pointerValue &&
        pointerValue.Children.Count == 2)
    {
        return $"global::FireURQ.Runtime.DelphiSystem.Dispose((IntPtr)({TranslateExpression(pointerValue.Children[1])}))";
    }

    if (_resolver is not null && _unitName is not null &&
        expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } interlocked &&
        interlocked.Text.ToUpperInvariant() is "INTERLOCKEDINCREMENT" or "INTERLOCKEDDECREMENT" &&
        !_resolver.HasVisibleValueDeclaration(interlocked.Text, _unitName))
    {
        string operation = interlocked.Text.Equals("InterlockedIncrement", StringComparison.OrdinalIgnoreCase)
            ? "Increment" : "Decrement";
        return $"global::System.Threading.Interlocked.{operation}(ref {TranslateExpression(expression.Children[1])})";
    }

    if (_resolver is not null && _unitName is not null &&
        expression.Children.Count == 3 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } formatTarget &&
        formatTarget.Text.Equals("Format", StringComparison.OrdinalIgnoreCase) &&
        !_resolver.HasVisibleValueDeclaration(formatTarget.Text, _unitName) &&
        ResolveExpressionType(formatTarget) is null &&
        expression.Children[2] is { Kind: ExpressionParser.ExpressionKind.Set } formatValues)
    {
        string format = TranslateExpression(expression.Children[1]);
        string values = string.Join(", ", formatValues.Children.Select(TranslateExpression));
        return $"Format({format}, new object[] {{ {values} }})";
    }

    if (expression.Children.Count == 3 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } formatFactory &&
        formatFactory.Children.Count == 2 &&
        formatFactory.Children[1].Text.Equals("CreateFmt", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[2] is { Kind: ExpressionParser.ExpressionKind.Set } factoryValues)
    {
        string factory = TranslateExpression(formatFactory);
        string format = TranslateExpression(expression.Children[1]);
        string values = string.Join(", ", factoryValues.Children.Select(value => TranslateValueOperand(value, out _)));
        if (formatFactory.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } exception &&
            exception.Text.Equals("Exception", StringComparison.OrdinalIgnoreCase) &&
            _resolver is not null && _unitName is not null && ResolveExpressionType(exception) is null &&
            !_resolver.HasVisibleValueDeclaration(exception.Text, _unitName) &&
            _resolver.ResolveDeclarationKind(exception.Text, _unitName) == "Unknown")
            return $"new global::System.Exception(global::FireURQ.Runtime.DelphiText.Format({format}, new object[] {{ {values} }}))";
        return $"{factory}({format}, new object[] {{ {values} }})";
    }

    if (_resolver is not null && _unitName is not null && expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } pointerOperation &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Identifier } pointerVariable &&
        ResolveExpressionType(pointerVariable) is string pointerType)
    {
        TypeResolution pointer = _resolver.Resolve(pointerType, _unitName);
        if (pointer.IsPointer && pointer.DeclaringUnitName is not null)
        {
            string variable = TranslateExpression(pointerVariable);
            if (pointerOperation.Text.Equals("New", StringComparison.OrdinalIgnoreCase) &&
                !_resolver.HasVisibleValueDeclaration(pointerOperation.Text, _unitName))
                return $"{variable} = global::FireURQ.Runtime.DelphiSystem.Allocate<{pointer.ResolvedName}>()";
        }
    }

    if (_resolver is not null && _unitName is not null && expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } pointerCast)
    {
        TypeResolution pointer = _resolver.Resolve(pointerCast.Text, _unitName);
        if (pointer.IsPointer && pointer.DeclaringUnitName is not null &&
            ResolveExpressionType(pointerCast) is null)
            return $"global::FireURQ.Runtime.DelphiSystem.Dereference<{pointer.ResolvedName}>({TranslateExpression(expression.Children[1])})";
    }

    if (_resolver is not null && _unitName is not null && expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } boundFunction &&
        boundFunction.Text.ToUpperInvariant() is "HIGH" or "LOW" &&
        ResolveExpressionType(boundFunction) is null &&
        !_resolver.HasVisibleValueDeclaration(boundFunction.Text, _unitName))
    {
        bool high = boundFunction.Text.Equals("High", StringComparison.OrdinalIgnoreCase);
        var operand = expression.Children[1];
        string? storageType = ResolveExpressionType(operand) ?? ResolveUnaryMemberType(operand)?.ResolvedName;
        if (storageType is null && operand.Kind == ExpressionParser.ExpressionKind.Identifier)
        {
            int length = _resolver.GetEnumerationLength(operand.Text, _unitName);
            if (length > 0)
                return $"(({MapType(operand.Text)}){(high ? length - 1 : 0)})";
        }
        if (storageType is not null)
        {
            if (_resolver.TryGetCompositeDefinition(storageType, _unitName, out string definition, out _))
                storageType = definition;
            if (storageType.TrimStart().StartsWith("array of ", StringComparison.OrdinalIgnoreCase))
                return high ? $"(({TranslateExpression(operand)}?.Length ?? 0) - 1)" : "0";
        }
    }

    if (_resolver is not null && _unitName is not null && expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } intrinsic &&
        intrinsic.Text.Equals("SizeOf", StringComparison.OrdinalIgnoreCase) &&
        !_resolver.HasVisibleValueDeclaration(intrinsic.Text, _unitName) &&
        ResolveExpressionType(intrinsic) is null &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Identifier } sizeOperand)
    {
        // SizeOf uses the declared Delphi storage type and does not evaluate its operand.
        string typeName = ResolveExpressionType(sizeOperand) ?? sizeOperand.Text;
        int recordSize = _resolver.FixedScalarRecordSize(typeName, _unitName);
        if (recordSize > 0)
            return recordSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var type = _resolver.Resolve(typeName, _unitName);
        if (type.IsResolved && !type.IsPointer && type.DeclaringUnitName is null)
        {
            string? size = type.ResolvedName.ToLowerInvariant() switch
            {
                "boolean" or "bytebool" or "byte" or "shortint" or "ansichar" or "char" => "1",
                "wordbool" or "word" or "smallint" or "widechar" => "2",
                "longbool" or "integer" or "longint" or "cardinal" or "longword" or "single" => "4",
                "int64" or "uint64" or "double" or "currency" or "comp" => "8",
                "extended" => "10",
                _ => null
            };
            if (size is not null)
                return size;
        }
    }

    if (string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 2 &&
        expression.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children[0].Text.Equals("VarToXSTR", StringComparison.OrdinalIgnoreCase) &&
        expression.Children[1] is { Kind: ExpressionParser.ExpressionKind.Call } cast &&
        cast.Children.Count == 2 &&
        cast.Children[0].Kind == ExpressionParser.ExpressionKind.Identifier &&
        cast.Children[0].Text.Equals("TVarData", StringComparison.OrdinalIgnoreCase))
        return $"VarToXSTR({TranslateExpression(cast.Children[1])})";

    if (string.Equals(_unitName, "SimpleXML", StringComparison.OrdinalIgnoreCase) &&
        TryTranslateSimpleXmlBinaryCall(expression, out string simpleXmlBinaryCall))
        return simpleXmlBinaryCall;

    if (string.Equals(_unitName, "f2FontGen", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count is 2 or 5 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.MemberAccess } bitmapCall &&
        bitmapCall.Children.Count == 2)
    {
        string method = bitmapCall.Children[1].Text;
        string bitmapTarget = TranslateExpression(bitmapCall.Children[0]);
        if (expression.Children.Count == 5 && method.Equals("LockBits", StringComparison.OrdinalIgnoreCase))
            return $"{bitmapTarget}.LockBits({TranslateExpression(expression.Children[1])}, {TranslateExpression(expression.Children[2])}, {TranslateExpression(expression.Children[3])}, out {TranslateExpression(expression.Children[4])})";
        if (expression.Children.Count == 2 && method.Equals("UnlockBits", StringComparison.OrdinalIgnoreCase))
            return $"{bitmapTarget}.UnlockBits(ref {TranslateExpression(expression.Children[1])})";
    }

    if (expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } numericHelper &&
        numericHelper.Text.Equals("EnsureReal", StringComparison.OrdinalIgnoreCase) &&
        _unitName?.ToUpperInvariant() is "FURQTYPES" or "FURQBASE" or "FURQCONTEXT" or
            "FURQEXPREVAL" or "F2CONTEXT" or "F2INTERPRETER" or "F2SCNMAIN")
        return $"global::furqTypesModule.EnsureReal({TranslateValueOperand(expression.Children[1], out _)})";

    if (_resolver is not null && _unitName is not null && expression.Children.Count == 2 &&
        expression.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } rounding &&
        rounding.Text.ToUpperInvariant() is "ROUND" or "TRUNC" &&
        ResolveExpressionType(rounding) is null && ResolveUnaryMemberType(rounding) is null &&
        _currentTypeContext?.ResolveLocalFunction(rounding.Text, 1, valueOnly: false).Found != true &&
        (_currentTypeContext?.OwnerClass is not { } roundingOwner ||
            _resolver.FindMember(roundingOwner, rounding.Text, _unitName, 1) is null) &&
        !_resolver.HasVisibleValueDeclaration(rounding.Text, _unitName))
    {
        string operation = rounding.Text.Equals("Round", StringComparison.OrdinalIgnoreCase) ? "Round" : "Trunc";
        return $"{operation}({TranslateValueOperand(expression.Children[1], out _)})";
    }

    if (TryTranslateDeclaredTypeCast(expression, out string declaredCast))
        return declaredCast;

    if (TryTranslateAnsiStringCall(expression, out string ansiStringCall))
        return ansiStringCall;

    if (expression.Children.Count == 0)
        return expression.Text;

    ExpressionParser.ExpressionNode targetExpression =
        expression.Children[0];

    // Preserve the address of a UInt32 size argument in Resource_Load.
    if (expression.Children.Count >= 3 &&
        expression.Children.Count <= 5 &&
        ((targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
          targetExpression.Text.Equals("Resource_Load", StringComparison.OrdinalIgnoreCase) &&
          string.Equals(_unitName, "d2dCore", StringComparison.OrdinalIgnoreCase)) ||
         (targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
          targetExpression.Children.Count == 2 &&
          targetExpression.Children[1].Text.Equals("Resource_Load", StringComparison.OrdinalIgnoreCase))) &&
        expression.Children[2] is { Kind: ExpressionParser.ExpressionKind.AddressOf } sizeAddress &&
        sizeAddress.Children.Count == 1 &&
        sizeAddress.Children[0] is { Kind: ExpressionParser.ExpressionKind.Identifier } sizeVariable &&
        MapType(ResolveExpressionType(sizeVariable)) == "uint")
    {
        string call = TranslateCallTarget(targetExpression);
        string resourceArguments = string.Join(", ", expression.Children.Skip(1)
            .Select((argument, index) => index == 1
                ? "__fireurqSizeAddress"
                : TranslateExpression(argument)));
        return "global::FireURQ.Runtime.DelphiGeometry.WithUInt32Address(" +
            $"ref {TranslateExpression(sizeVariable)}, __fireurqSizeAddress => " +
            $"{call}({resourceArguments}))";
    }

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

        if (method.Equals("SetViewport", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 2)
        {
            return $"f_D3DDevice.SetViewport(ref {TranslateExpression(expression.Children[1])})";
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

    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("GetWindowRect", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 3 &&
        _resolver is not null && !string.IsNullOrEmpty(_unitName) &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName) &&
        ResolveExpressionType(targetExpression) is null &&
        string.Equals(MapType(ResolveExpressionType(expression.Children[2]) ?? ResolveUnaryMemberType(expression.Children[2])?.ResolvedName), "TRect", StringComparison.OrdinalIgnoreCase))
    {
        string window = TranslateExpression(expression.Children[1]);
        string rectangle = TranslateExpression(expression.Children[2]);
        return $"GetWindowRect({window}, out {rectangle})";
    }

    // IDirect3DTexture8.GetLevelDesc writes the descriptor through its second parameter.
    if (expression.Children.Count == 3 &&
        targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess &&
        targetExpression.Children.Count == 2 &&
        targetExpression.Children[1].Text.Equals("GetLevelDesc", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(MapType(ResolveExpressionType(targetExpression.Children[0])), "IDirect3DTexture8", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(MapType(ResolveExpressionType(expression.Children[2])), "D3DSURFACE_DESC", StringComparison.OrdinalIgnoreCase))
    {
        string receiver = TranslateExpression(targetExpression.Children[0]);
        string level = TranslateExpression(expression.Children[1]);
        string descriptor = TranslateExpression(expression.Children[2]);
        return $"{receiver}.GetLevelDesc({level}, out {descriptor})";
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
            bufferCall.Children[3].Text.Equals("D3DFVF_D2DVERTEX", StringComparison.OrdinalIgnoreCase))
        {
            return "(f_D3DDevice.CreateVertexBuffer(" +
                $"checked((uint)({length})), " +
                "0x08u, 0x0142u, 0, out f_VB) < 0)";
        }

        if (method.Equals("CreateIndexBuffer", StringComparison.OrdinalIgnoreCase) &&
            destination.Equals("f_IB", StringComparison.OrdinalIgnoreCase) &&
            bufferCall.Children[3].Text.Equals("D3DFMT_INDEX16", StringComparison.OrdinalIgnoreCase))
        {
            return "(f_D3DDevice.CreateIndexBuffer(" +
                $"checked((uint)({length})), " +
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
        if (streamMethod.Equals("SetRenderState", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 3)
        {
            string state = TranslateExpression(expression.Children[1]);
            string value = TranslateExpression(expression.Children[2]);
            return "global::FireURQ.Runtime.D3D8.SetRenderState(f_D3DDevice, " +
                $"{state}, unchecked((uint)({value})))";
        }

        if (streamMethod.Equals("SetTexture", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 3)
        {
            string stage = TranslateExpression(expression.Children[1]);
            string texture = TranslateExpression(expression.Children[2]);
            return "global::FireURQ.Runtime.D3D8.SetTexture(f_D3DDevice, " +
                $"unchecked((uint)({stage})), {texture})";
        }

        if (streamMethod.Equals("SetTextureStageState", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 4)
        {
            string stage = TranslateExpression(expression.Children[1]);
            string state = TranslateExpression(expression.Children[2]);
            string value = TranslateExpression(expression.Children[3]);
            return "global::FireURQ.Runtime.D3D8.SetTextureStageState(f_D3DDevice, " +
                $"unchecked((uint)({stage})), {state}, unchecked((uint)({value})))";
        }

        if (streamMethod.Equals("SetStreamSource", StringComparison.OrdinalIgnoreCase) &&
            expression.Children.Count == 4)
        {
            string streamNumber = TranslateExpression(expression.Children[1]);
            string vertexBuffer = TranslateExpression(expression.Children[2]);
            return "global::FireURQ.Runtime.D3D8.SetStreamSource(f_D3DDevice, " +
                $"checked((uint)({streamNumber})), {vertexBuffer}, " +
                $"checked((uint)({TranslateExpression(expression.Children[3])})))";
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

    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        expression.Children.Count == 2 &&
        targetExpression.Text.Equals("VarClear", StringComparison.OrdinalIgnoreCase) &&
        _resolver is not null && _unitName is not null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName) &&
        ResolveExpressionType(targetExpression) is null)
        return $"global::FireURQ.Runtime.DelphiVariants.VarClear(ref {TranslateExpression(expression.Children[1])})";

    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("FreeAndNil", StringComparison.OrdinalIgnoreCase) &&
        expression.Children.Count == 2 && _unitName is not null && _resolver is not null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName) &&
        ResolveExpressionType(targetExpression) is null)
    {
        var variable = expression.Children[1];
        bool arraySlot = variable.Kind == ExpressionParser.ExpressionKind.IndexAccess &&
            variable.Children.Count >= 2 &&
            MapType(ResolveExpressionType(variable.Children[0]) ??
                ResolveUnaryMemberType(variable.Children[0])?.ResolvedName).EndsWith("]", StringComparison.Ordinal);
        if (arraySlot || variable.Kind is ExpressionParser.ExpressionKind.Identifier or ExpressionParser.ExpressionKind.MemberAccess)
            return $"global::FireURQ.Runtime.TObject.FreeAndNil(ref {TranslateExpression(variable)})";
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

    var signature = targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        _currentTypeContext?.OwnerClass is null && ResolveExpressionType(targetExpression) is null
        ? _resolver?.FindUnitProcedureVariable(targetExpression.Text, _unitName)
        : null;

    IReadOnlyList<SemanticParameter>? formals = signature?.Parameters;
    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        _currentTypeContext?.ResolveLocalFunction(targetExpression.Text,
            expression.Children.Count - 1, valueOnly: false).Function is { } nestedRoutine)
    {
        formals = SemanticModelBuilder.BuildFunction(nestedRoutine).Parameters;
        target = nestedRoutine.Name;
    }
    if (TryGetConstructorFactory(targetExpression, expression.Children.Count - 1, out string constructorFactory, out var constructorParameters))
    {
        target = constructorFactory;
        formals = constructorParameters;
    }
    if (formals is null && _resolver is not null)
    {
        string? receiver = null;
        string? receiverUnit = _unitName;
        string method = targetExpression.Text;
        if (targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess && targetExpression.Children.Count == 2)
        {
            var instance = targetExpression.Children[0];
            method = targetExpression.Children[1].Text;
            receiver = ResolveExpressionType(instance);
            if (receiver is null && ResolveUnaryMemberType(instance) is { IsResolved: true, IsPointer: false } field)
            {
                receiver = field.ResolvedName;
                receiverUnit = field.DeclaringUnitName ?? _unitName;
            }
        }
        else if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
            ResolveExpressionType(targetExpression) is null &&
            _currentTypeContext?.ResolveLocalFunction(method, expression.Children.Count - 1).Found != true)
            receiver = _currentTypeContext?.OwnerClass;

        if (receiver is not null && _resolver.FindMember(receiver, method, receiverUnit, expression.Children.Count - 1) is { } binding)
        {
            formals = binding.Member.Parameters;
            target = targetExpression.Kind == ExpressionParser.ExpressionKind.MemberAccess
                ? TranslateValueOperand(targetExpression.Children[0], out _) + "." + binding.Member.Name
                : binding.Member.Name;
        }
    }

    if (formals is null && targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        ResolveExpressionType(targetExpression) is null &&
        ResolveUnaryMemberType(targetExpression) is null &&
        _currentTypeContext?.ResolveLocalFunction(targetExpression.Text, expression.Children.Count - 1).Found != true)
        formals = _resolver?.FindUnitRoutineParameters(targetExpression.Text, _unitName, expression.Children.Count - 1);

    if (formals is null && _resolver is not null &&
        targetExpression.Kind is ExpressionParser.ExpressionKind.Identifier or ExpressionParser.ExpressionKind.MemberAccess)
    {
        string? declared = ResolveExpressionType(targetExpression);
        var dataType = declared is null ? ResolveUnaryMemberType(targetExpression) : _resolver.Resolve(declared, _unitName);
        if (dataType is { IsResolved: true, IsPointer: false })
            formals = _resolver.ResolveProcedureType(dataType.ResolvedName,
                dataType.DeclaringUnitName ?? _unitName)?.Parameters;
    }

    var arguments = new List<string>();
    for (int i = 1; i < expression.Children.Count; i++)
    {
        var argument = expression.Children[i];
        var formal = formals?.Count >= expression.Children.Count - 1 ? formals[i - 1] : null;
        string value = formal is not null && !formal.Kind.Equals("var", StringComparison.OrdinalIgnoreCase) &&
            !formal.Kind.Equals("out", StringComparison.OrdinalIgnoreCase)
            ? TranslateExpectedValue(argument, formal.Type) : TranslateExpression(argument);
        // Delphi types a real literal from its formal parameter. An unsuffixed
        // fractional C# literal would instead be a double.
        if (formals?.Count >= expression.Children.Count - 1 &&
            MapType(formals[i - 1].Type) == "float" &&
            argument.Kind == ExpressionParser.ExpressionKind.Literal &&
            (value.Contains('.') || value.Contains('e', StringComparison.OrdinalIgnoreCase)) &&
            float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            value += "f";

        if (formals?.Count >= expression.Children.Count - 1)
        {
            string mode = formals[i - 1].Kind;
            if (mode.Equals("var", StringComparison.OrdinalIgnoreCase)) value = "ref " + value;
            if (mode.Equals("out", StringComparison.OrdinalIgnoreCase)) value = "out " + value;
        }
        arguments.Add(value);
    }

    int suppliedArguments = expression.Children.Count - 1;
    if (formals is not null && formals.Count > suppliedArguments)
    {
        foreach (SemanticParameter parameter in formals.Skip(suppliedArguments))
            arguments.Add(TranslateDefaultArgument(parameter));
    }

    // Delphi Assigned(P) is a null/non-null test, including the common
    // Assigned(@ProcedureVariable) spelling used after GetProcAddress.
    if (IsAssignedCall(expression))
    {
        ExpressionParser.ExpressionNode argument =
            UnwrapAddressOfIdentifier(expression.Children[1]);

        string assignedValue = TranslateExpression(argument);
        string nullValue = IsIntPtrBackedExpression(argument)
            ? "IntPtr.Zero"
            : "null";
        return $"({assignedValue} != {nullValue})";
    }

    if (targetExpression.Kind == ExpressionParser.ExpressionKind.Identifier &&
        targetExpression.Text.Equals("Dec", StringComparison.OrdinalIgnoreCase) &&
        arguments.Count is 1 or 2 && _resolver is not null && _unitName is not null &&
        ResolveExpressionType(targetExpression) is null &&
        !_resolver.HasVisibleValueDeclaration(targetExpression.Text, _unitName))
    {
        var operand = expression.Children[1];
        string? localType = ResolveExpressionType(operand);
        var resolved = localType is null ? ResolveUnaryMemberType(operand) : _resolver.Resolve(localType, _unitName);
        if (resolved is { IsResolved: true, IsPointer: false } && MapType(resolved.ResolvedName) is
            "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong")
            return arguments.Count == 1
                ? $"{arguments[0]}--"
                : $"{arguments[0]} -= {arguments[1]}";
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
            TranslateValueOperand(expression.Children[0], out _);

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
            TranslateValueOperand(expression.Children[0], out _);

        string right =
            TranslateExpression(
                expression.Children[1]);

        return $"({left} as {right})";
    }

    private string TranslateCharacterSet(ExpressionParser.ExpressionNode expression)
    {
        var scalar = new List<string>();
        var ranges = new List<string>();
        foreach (ExpressionParser.ExpressionNode element in expression.Children)
        {
            if (element.Kind == ExpressionParser.ExpressionKind.Binary &&
                element.Text == ".." && element.Children.Count == 2)
            {
                string lower = TranslateExpectedValue(element.Children[0], "Char");
                string upper = TranslateExpectedValue(element.Children[1], "Char");
                ranges.Add($"Enumerable.Range((int)({lower}), (int)({upper}) - (int)({lower}) + 1).Select(code => (char)code)");
            }
            else
            {
                scalar.Add(TranslateExpectedValue(element, "Char"));
            }
        }

        string values = scalar.Count == 0
            ? "Enumerable.Empty<char>()"
            : $"new char[] {{ {string.Join(", ", scalar)} }}";
        foreach (string range in ranges)
            values += $".Concat({range})";
        return $"new HashSet<char>({values})";
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
                string lower = TranslateExpectedValue(element.Children[0], "Char");
                string upper = TranslateExpectedValue(element.Children[1], "Char");

                ranges.Add(
                    $"Enumerable.Range((int)({lower}), (int)({upper}) - (int)({lower}) + 1)");
                continue;
            }

            string value = TranslateExpectedValue(element, "Char");
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
            string translatedSet = TranslateExpression(set);
            var operand = expression.Children[0];
            string? valueType = ResolveExpressionType(operand) ?? ResolveUnaryMemberType(operand)?.ResolvedName;
            string mapped = MapType(valueType);
            if (mapped is "byte" or "char" or "int")
                return $"{translatedSet}.Contains({value})";
            string? setType = ResolveExpressionType(set) ?? ResolveUnaryMemberType(set)?.ResolvedName;
            if (setType is not null && MapType(setType) == $"HashSet<{mapped}>")
                return $"{translatedSet}.Contains({value})";
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
                        TranslateExpectedValue(element.Children[0], "Char");

                    string upper =
                        TranslateExpectedValue(element.Children[1], "Char");

                    conditions.Add(
                        $"({value} >= {lower} && {value} <= {upper})");
                }

                continue;
            }

            string translatedElement =
                TranslateExpectedValue(element, "Char");

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

private static bool TryTranslatePascalStringLiteral(string text, out string result, bool preferChar = false)
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

    if ((preferChar || text[0] == '#') && characters.Count == 1)
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
        private readonly Dictionary<string, (string Name, string Type)> _identifiers =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly string _returnType;
        private readonly FunctionTypeContext? _parent;
        private readonly IReadOnlyList<DelphiFunction> _localFunctions;
        private readonly int _depth;
        public string ResultName { get; }
        public string? OwnerClass { get; }
        public Dictionary<string, string> ConstCopies { get; } = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _useCapturedCopyInBody =
            new(StringComparer.OrdinalIgnoreCase);

        public FunctionTypeContext(SemanticFunction function, FunctionTypeContext? parent, string? ownerClass)
        {
            _parent = parent;
            _localFunctions = function.LocalFunctions;
            OwnerClass = ownerClass ?? parent?.OwnerClass;
            _depth = parent is null ? 0 : parent._depth + 1;
            ResultName = function.IsProcedure && parent is not null ? parent.ResultName :
                _depth == 0 ? "result" : $"__localResult{_depth}";
            foreach (
                SemanticParameter parameter
                in function.Parameters)
            {
                _identifiers[parameter.Name] = (parameter.Name, parameter.Type);
            }

            foreach (
                SemanticVariable variable
                in function.LocalVariables)
            {
                _identifiers[variable.Name] = (variable.Name, variable.Type);
            }

            foreach (var parameter in function.Parameters)
            {
                bool isConst =
                    parameter.Kind.Equals("const", StringComparison.OrdinalIgnoreCase);
                bool needsLambdaValueCopy =
                    function.Name.Equals("Texture_Load", StringComparison.OrdinalIgnoreCase) &&
                    parameter.Name.Equals("aFileName", StringComparison.OrdinalIgnoreCase) &&
                    isConst;
                bool isCapturedReferenceList =
                    function.Name.Equals("ParseLocAndParams", StringComparison.OrdinalIgnoreCase) &&
                    parameter.Name.Equals("Params", StringComparison.OrdinalIgnoreCase) &&
                    parameter.Kind.Equals("var", StringComparison.OrdinalIgnoreCase) &&
                    function.LocalFunctions.Count != 0;

                if ((!isConst || function.LocalFunctions.Count == 0) &&
                    !needsLambdaValueCopy &&
                    !isCapturedReferenceList)
                    continue;

                string copy = $"__captured{_depth}_{parameter.Name}";
                while (_identifiers.ContainsKey(copy) || parent?.ResolveIdentifier(copy) is not null)
                    copy += "_";
                ConstCopies.Add(parameter.Name, copy);
                _identifiers.Add(copy, (copy, parameter.Type));
                if (isConst)
                    _useCapturedCopyInBody.Add(parameter.Name);
            }

            _returnType = function.IsProcedure && parent is not null ? parent._returnType : function.ReturnType;
        }

        public void AddWithAlias(string name, string type) => _identifiers.Add(name, (name, type));
        public void RemoveWithAlias(string name) => _identifiers.Remove(name);

        public (bool Found, DelphiFunction? Function) ResolveLocalFunction(string name, int argumentCount, bool valueOnly = true)
        {
            if (_identifiers.ContainsKey(name))
                return (true, null);

            var named = _localFunctions.Where(function =>
                function.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (named.Length == 0)
                return _parent?.ResolveLocalFunction(name, argumentCount, valueOnly) ?? (false, null);

            var matching = named.Where(function =>
                (!valueOnly || !function.IsProcedure && !string.IsNullOrWhiteSpace(function.ReturnType)) &&
                function.Parameters.Count >= argumentCount &&
                function.Parameters.Skip(argumentCount).All(parameter =>
                    !string.IsNullOrWhiteSpace(parameter.DefaultValue))).ToArray();
            return (true, matching.Length == 1 ? matching[0] : null);
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

            return _identifiers.TryGetValue(name, out var symbol)
                ? symbol.Type
                : _parent?.ResolveIdentifier(name);
        }

        public string ResolveSpelling(string name)
        {
            if (_useCapturedCopyInBody.Contains(name) &&
                ConstCopies.TryGetValue(name, out string? copy))
                return copy;
            if (_identifiers.TryGetValue(name, out var symbol))
                return symbol.Name;
            return _parent?.ResolveCapturedSpelling(name) ?? name;
        }

        private string ResolveCapturedSpelling(string name) =>
            ConstCopies.TryGetValue(name, out string? copy) ? copy : ResolveSpelling(name);
    }
}
