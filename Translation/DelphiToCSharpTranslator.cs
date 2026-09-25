using System.Text;
using FireURQ.Porter.Analysis;

namespace FireURQ.Porter.Translation;

public sealed class DelphiToCSharpTranslator
{
    public string TranslateFunction(
        SemanticFunction function,
        BehaviorAnalyzer.BehaviorInfo behavior)
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

foreach (SemanticVariable variable
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

foreach (BehaviorAnalyzer.BehaviorStatement statement
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

    private static void TranslateStatement(
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

            case BehaviorAnalyzer.StatementKind.Block:
                foreach (BehaviorAnalyzer.BehaviorStatement child
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

private static void TranslateCase(
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

private static void TranslateIf(
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

    foreach (BehaviorAnalyzer.BehaviorStatement child
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

        foreach (BehaviorAnalyzer.BehaviorStatement child
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

private static void TranslateWhile(
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

    foreach (BehaviorAnalyzer.BehaviorStatement child
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

private static void TranslateFor(
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

    foreach (BehaviorAnalyzer.BehaviorStatement child
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

private static string TranslateIdentifier(
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

    private static void TranslateAssignment(
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

    private static string TranslateExpression(
        ExpressionParser.ExpressionNode? expression)
    {
        if (expression is null)
            return string.Empty;

        return expression.Kind switch
        {
            ExpressionParser.ExpressionKind.Empty =>
                string.Empty,

ExpressionParser.ExpressionKind.Identifier =>
    TranslateIdentifier(
        expression.Text),

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

            _ =>
                expression.Text
        };
    }

private static string TranslateBinary(
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

    string op =
        expression.Text.ToLowerInvariant() switch
        {
            "=" => "==",
            "<>" => "!=",
            "and" => "&&",
            "or" => "||",
            "div" => "/",
            "mod" => "%",
            _ => expression.Text
        };

    return $"({left} {op} {right})";
}

private static string TranslateUnary(
    ExpressionParser.ExpressionNode expression)
{
    if (expression.Children.Count == 0)
        return expression.Text;

    string operand =
        TranslateExpression(
            expression.Children[0]);

    string op =
        expression.Text.ToLowerInvariant() switch
        {
            "not" => "!",
            "+" => "+",
            "-" => "-",
            _ => expression.Text
        };

    return $"{op}{operand}";
}

    private static string TranslateMemberAccess(
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

    private static string TranslateIndexAccess(
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

    private static string TranslateCall(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count == 0)
            return expression.Text;

        string target =
            TranslateExpression(
                expression.Children[0]);

        var arguments = new List<string>();

        for (int i = 1; i < expression.Children.Count; i++)
        {
            arguments.Add(
                TranslateExpression(
                    expression.Children[i]));
        }

        return $"{target}({string.Join(", ", arguments)})";
    }

    private static string TranslateLiteral(string text)
    {
        return text.ToLowerInvariant() switch
        {
            "true" => "true",
            "false" => "false",
            "nil" => "null",
            _ => text
        };
    }

    private static string MapParameterKind(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "var" => "ref ",
            "out" => "out ",
            "const" => "",
            _ => ""
        };
    }

    private static void AppendLine(
        StringBuilder builder,
        int indent,
        string text)
    {
        builder.Append(' ', indent * 4);
        builder.AppendLine(text);
    }

    private void TranslateCase(
    BehaviorAnalyzer.BehaviorStatement statement,
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

                continue;
            }

            AppendLine(
                builder,
                indent + 1,
                $"case {label}:");
        }

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in branch.Children)
        {
            TranslateStatement(
                child,
                null!,
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
}
