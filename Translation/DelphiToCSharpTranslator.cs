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
            statement.Expression is null
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
        AppendLine(
            builder,
            indent,
            "try");

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

        if (statement.Clauses is null)
            return;

        foreach (
            BehaviorAnalyzer.BehaviorClause clause
            in statement.Clauses)
        {
            if (clause.Kind.Equals(
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

        string target =
            TranslateExpression(
                expression.Children[0]);

        var arguments = new List<string>();

        for (int i = 1;
             i < expression.Children.Count;
             i++)
        {
            arguments.Add(
                TranslateExpression(
                    expression.Children[i]));
        }

        return $"{target}({string.Join(", ", arguments)})";
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
            return "[]";

        var elements =
            expression.Children
                .Select(TranslateExpression)
                .ToList();

        return $"[{string.Join(", ", elements)}]";
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
            return
                $"({value} /* in */ {TranslateExpression(set)})";
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
        if (text.StartsWith("'") &&
            text.EndsWith("'"))
        {
            string content =
                text[1..^1];

            content =
                content.Replace(
                    "''",
                    "'");

            return
                $"\"{content.Replace("\"", "\\\"")}\"";
        }

        if (text.Equals(
                "nil",
                StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        if (text.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase) ||
            text.Equals(
                "false",
                StringComparison.OrdinalIgnoreCase))
        {
            return text.ToLowerInvariant();
        }

        return text;
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
