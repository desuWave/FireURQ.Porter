using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class DelphiToCSharpTranslator
{
    private FunctionTypeContext? _currentTypeContext;

    public string TranslateFunction(
        SemanticFunction function,
        BehaviorAnalyzer.BehaviorInfo behavior)
    {
        FunctionTypeContext? previous =
            _currentTypeContext;

        _currentTypeContext =
            new FunctionTypeContext(function);

        try
        {
            var builder = new StringBuilder();

            string returnType =
                function.IsProcedure
                    ? "void"
                    : CSharpTypeMapper.Map(function.ReturnType);

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

                builder
                    .Append(MapParameterKind(parameter.Kind))
                    .Append(CSharpTypeMapper.Map(parameter.Type))
                    .Append(' ')
                    .Append(parameter.Name);
            }

            builder.AppendLine(")");
            builder.AppendLine("{");

            foreach (
                SemanticVariable variable
                in function.LocalVariables)
            {
                builder.AppendLine(
                    $"    {CSharpTypeMapper.Map(variable.Type)} " +
                    $"{variable.Name};");
            }

            if (!function.IsProcedure)
            {
                builder.AppendLine(
                    $"    {CSharpTypeMapper.Map(function.ReturnType)} result;");
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
                    "return result;");
            }

            builder.AppendLine("}");

            return builder.ToString();
        }
        finally
        {
            _currentTypeContext = previous;
        }
    }

    private void TranslateStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        SemanticFunction function,
        StringBuilder builder,
        int indent)
    {
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
                    TranslateExpression(
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
                        : "return result;");
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
                        $"case {label}:");
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
        if (text.Equals(
            "Result",
            StringComparison.OrdinalIgnoreCase))
        {
            return "result";
        }

        return text;
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

        string target =
            TranslateExpression(
                statement.Assignment.Target);

        string value =
            TranslateExpression(
                statement.Assignment.Value);

        if (!function.IsProcedure &&
            target.Equals(
                "Result",
                StringComparison.OrdinalIgnoreCase))
        {
            target = "result";
        }

        AppendLine(
            builder,
            indent,
            $"{target} = {value};");
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

    private string TranslateExpression(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null)
            return string.Empty;

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
            string.Equals(
                leftType,
                "Boolean",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                rightType,
                "Boolean",
                StringComparison.OrdinalIgnoreCase);

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

        bool isBoolean =
            string.Equals(
                operandType,
                "Boolean",
                StringComparison.OrdinalIgnoreCase);

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

private string TranslateCall(
    ExpressionParser.ExpressionNode expression)
{
    if (expression.Children.Count == 0)
        return expression.Text;

    ExpressionParser.ExpressionNode targetExpression =
        expression.Children[0];

    string target =
        TranslateExpression(
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
            CSharpTypeMapper.Map(
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

        public FunctionTypeContext(
            SemanticFunction function)
        {
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

            _returnType =
                function.ReturnType;
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
                : null;
        }
    }
}
