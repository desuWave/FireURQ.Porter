namespace FireURQ.Porter.Analysis;

public static class BehaviorMetricsAnalyzer
{
    public sealed record Metrics(
        int Assignments,
        int Calls,
        int Conditions,
        int Loops,
        int Expressions,
        int Identifiers,
        int Literals,
        int MemberAccesses,
        int IndexAccesses,
        int UnaryExpressions,
        int BinaryExpressions);

    public static Metrics Analyze(
        BehaviorAnalyzer.BehaviorInfo behavior)
    {
        int assignments = 0;
        int calls = 0;
        int conditions = 0;
        int loops = 0;
        int expressions = 0;

        int identifiers = 0;
        int literals = 0;
        int memberAccesses = 0;
        int indexAccesses = 0;
        int unaryExpressions = 0;
        int binaryExpressions = 0;

        foreach (
            BehaviorAnalyzer.BehaviorStatement statement
            in behavior.Statements)
        {
            AnalyzeStatement(
                statement,
                ref assignments,
                ref calls,
                ref conditions,
                ref loops,
                ref expressions,
                ref identifiers,
                ref literals,
                ref memberAccesses,
                ref indexAccesses,
                ref unaryExpressions,
                ref binaryExpressions);
        }

        return new Metrics(
            assignments,
            calls,
            conditions,
            loops,
            expressions,
            identifiers,
            literals,
            memberAccesses,
            indexAccesses,
            unaryExpressions,
            binaryExpressions);
    }

    private static void AnalyzeStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        ref int assignments,
        ref int calls,
        ref int conditions,
        ref int loops,
        ref int expressions,
        ref int identifiers,
        ref int literals,
        ref int memberAccesses,
        ref int indexAccesses,
        ref int unaryExpressions,
        ref int binaryExpressions)
    {
        switch (statement.Kind)
        {
            case BehaviorAnalyzer.StatementKind.Assignment:
                assignments++;
                break;

            case BehaviorAnalyzer.StatementKind.If:
            case BehaviorAnalyzer.StatementKind.Case:
                conditions++;
                break;

            case BehaviorAnalyzer.StatementKind.While:
            case BehaviorAnalyzer.StatementKind.For:
            case BehaviorAnalyzer.StatementKind.Repeat:
                loops++;
                break;

            case BehaviorAnalyzer.StatementKind.Expression:
            case BehaviorAnalyzer.StatementKind.Call:
                expressions++;
                break;
        }

        if (statement.Assignment != null)
        {
            AnalyzeExpression(
                statement.Assignment.Target,
                ref calls,
                ref identifiers,
                ref literals,
                ref memberAccesses,
                ref indexAccesses,
                ref unaryExpressions,
                ref binaryExpressions);

            AnalyzeExpression(
                statement.Assignment.Value,
                ref calls,
                ref identifiers,
                ref literals,
                ref memberAccesses,
                ref indexAccesses,
                ref unaryExpressions,
                ref binaryExpressions);
        }

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            AnalyzeStatement(
                child,
                ref assignments,
                ref calls,
                ref conditions,
                ref loops,
                ref expressions,
                ref identifiers,
                ref literals,
                ref memberAccesses,
                ref indexAccesses,
                ref unaryExpressions,
                ref binaryExpressions);
        }

        if (statement.ElseChildren != null)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in statement.ElseChildren)
            {
                AnalyzeStatement(
                    child,
                    ref assignments,
                    ref calls,
                    ref conditions,
                    ref loops,
                    ref expressions,
                    ref identifiers,
                    ref literals,
                    ref memberAccesses,
                    ref indexAccesses,
                    ref unaryExpressions,
                    ref binaryExpressions);
            }
        }

        if (statement.Clauses != null)
        {
            foreach (
                BehaviorAnalyzer.BehaviorClause clause
                in statement.Clauses)
            {
                foreach (
                    BehaviorAnalyzer.BehaviorStatement child
                    in clause.Statements)
                {
                    AnalyzeStatement(
                        child,
                        ref assignments,
                        ref calls,
                        ref conditions,
                        ref loops,
                        ref expressions,
                        ref identifiers,
                        ref literals,
                        ref memberAccesses,
                        ref indexAccesses,
                        ref unaryExpressions,
                        ref binaryExpressions);
                }
            }
        }
    }

    private static void AnalyzeExpression(
        ExpressionParser.ExpressionNode expression,
        ref int calls,
        ref int identifiers,
        ref int literals,
        ref int memberAccesses,
        ref int indexAccesses,
        ref int unaryExpressions,
        ref int binaryExpressions)
    {
        switch (expression.Kind)
        {
            case ExpressionParser.ExpressionKind.Identifier:
                identifiers++;
                break;

            case ExpressionParser.ExpressionKind.Literal:
                literals++;
                break;

            case ExpressionParser.ExpressionKind.Call:
                calls++;
                break;

            case ExpressionParser.ExpressionKind.MemberAccess:
                memberAccesses++;
                break;

            case ExpressionParser.ExpressionKind.IndexAccess:
                indexAccesses++;
                break;

            case ExpressionParser.ExpressionKind.Unary:
                unaryExpressions++;
                break;

            case ExpressionParser.ExpressionKind.Binary:
                binaryExpressions++;
                break;
        }

        foreach (
            ExpressionParser.ExpressionNode child
            in expression.Children)
        {
            AnalyzeExpression(
                child,
                ref calls,
                ref identifiers,
                ref literals,
                ref memberAccesses,
                ref indexAccesses,
                ref unaryExpressions,
                ref binaryExpressions);
        }
    }
}