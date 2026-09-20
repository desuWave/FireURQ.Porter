namespace FireURQ.Porter.Analysis;

public static class ExpressionCensus
{
    public sealed record Counts(
        int TotalNodes,
        int StatementsWithExpressions,
        IReadOnlyDictionary<
            ExpressionParser.ExpressionKind,
            int> ByKind,
        IReadOnlyDictionary<
            string,
            int> RawExpressions);

    public static Counts Analyze(
        IReadOnlyList<BehaviorAnalyzer.BehaviorInfo> behaviors)
    {
        var counts =
            new Dictionary<
                ExpressionParser.ExpressionKind,
                int>();

        var rawExpressions =
            new Dictionary<
                string,
                int>(
                StringComparer.Ordinal);

        int totalNodes = 0;
        int statementsWithExpressions = 0;

        foreach (
            BehaviorAnalyzer.BehaviorInfo behavior
            in behaviors)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement statement
                in behavior.Statements)
            {
                AnalyzeStatement(
                    statement,
                    counts,
                    rawExpressions,
                    ref totalNodes,
                    ref statementsWithExpressions,
                    behavior.FilePath,
                    behavior.Name);
            }
        }

        return new Counts(
            totalNodes,
            statementsWithExpressions,
            counts,
            rawExpressions);
    }

    public static void Print(
        Counts counts)
    {
        Console.WriteLine();

        Console.WriteLine(
            "================================");

        Console.WriteLine(
            " Expression Census");

        Console.WriteLine(
            "================================");

        Console.WriteLine();

        Console.WriteLine(
            $"Expression nodes:           " +
            $"{counts.TotalNodes}");

        Console.WriteLine(
            $"Statements with expression: " +
            $"{counts.StatementsWithExpressions}");

        Console.WriteLine();

        Console.WriteLine(
            "By kind");

        Console.WriteLine(
            "--------------------------------");

        foreach (
            ExpressionParser.ExpressionKind kind
            in Enum.GetValues<
                ExpressionParser.ExpressionKind>())
        {
            counts.ByKind.TryGetValue(
                kind,
                out int count);

            Console.WriteLine(
                $"{kind,-16} {count,6}");
        }

        int structured =
            counts.ByKind
                .Where(
                    pair =>
                        pair.Key !=
                        ExpressionParser.ExpressionKind.Raw)
                .Sum(
                    pair =>
                        pair.Value);

        int raw =
            counts.ByKind.TryGetValue(
                ExpressionParser.ExpressionKind.Raw,
                out int rawCount)
                ? rawCount
                : 0;

        double structuredCoverage =
            counts.TotalNodes == 0
                ? 100.0
                : structured * 100.0 /
                  counts.TotalNodes;

        double rawShare =
            counts.TotalNodes == 0
                ? 0.0
                : raw * 100.0 /
                  counts.TotalNodes;

        Console.WriteLine();

        Console.WriteLine(
            $"Structured:                  {structured}");

        Console.WriteLine(
            $"Raw:                         {raw}");

        Console.WriteLine(
            $"Structured coverage:         " +
            $"{structuredCoverage:F1}%");

        Console.WriteLine(
            $"Raw share:                   " +
            $"{rawShare:F1}%");

        if (counts.RawExpressions.Count > 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Raw expressions");

            Console.WriteLine(
                "--------------------------------");

            int index = 1;

            foreach (
                KeyValuePair<string, int> pair
                in counts.RawExpressions
                    .OrderByDescending(
                        pair => pair.Value)
                    .ThenBy(
                        pair => pair.Key,
                        StringComparer.Ordinal))
            {
                Console.WriteLine(
                    $"{index,3}. " +
                    $"{pair.Value,3}x  " +
                    $"{pair.Key}");

                index++;
            }

            Console.WriteLine();

            Console.WriteLine(
                $"Unique Raw expressions: " +
                $"{counts.RawExpressions.Count}");
        }

        Console.WriteLine();
    }

    private static void AnalyzeStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        Dictionary<
            ExpressionParser.ExpressionKind,
            int> counts,
        Dictionary<string, int> rawExpressions,
        ref int totalNodes,
        ref int statementsWithExpressions,
        string filePath,
        string procedureName)
    {
        bool hasExpression = false;

        if (statement.Expression is not null)
        {
            hasExpression = true;

            CountExpression(
                statement.Expression,
                counts,
                rawExpressions,
                ref totalNodes,
                filePath,
                procedureName,
                statement.Line);
        }

        if (statement.Assignment is not null)
        {
            hasExpression = true;

            CountExpression(
                statement.Assignment.Target,
                counts,
                rawExpressions,
                ref totalNodes,
                filePath,
                procedureName,
                statement.Line);

            CountExpression(
                statement.Assignment.Value,
                counts,
                rawExpressions,
                ref totalNodes,
                filePath,
                procedureName,
                statement.Line);
        }

        if (hasExpression)
        {
            statementsWithExpressions++;
        }

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            AnalyzeStatement(
                child,
                counts,
                rawExpressions,
                ref totalNodes,
                ref statementsWithExpressions,
                filePath,
                procedureName);
        }

        if (statement.ElseChildren is not null)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in statement.ElseChildren)
            {
                AnalyzeStatement(
                    child,
                    counts,
                    rawExpressions,
                    ref totalNodes,
                    ref statementsWithExpressions,
                    filePath,
                    procedureName);
            }
        }

        if (statement.Clauses is not null)
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
                        counts,
                        rawExpressions,
                        ref totalNodes,
                        ref statementsWithExpressions,
                        filePath,
                        procedureName);
                }
            }
        }
    }

    private static void CountExpression(
        ExpressionParser.ExpressionNode expression,
        Dictionary<
            ExpressionParser.ExpressionKind,
            int> counts,
        Dictionary<string, int> rawExpressions,
        ref int totalNodes,
        string? filePath = null,
        string? procedureName = null,
        int line = 0)
    {
        totalNodes++;

        if (counts.TryGetValue(
                expression.Kind,
                out int count))
        {
            counts[expression.Kind] =
                count + 1;
        }
        else
        {
            counts[expression.Kind] = 1;
        }

        if (expression.Kind ==
            ExpressionParser.ExpressionKind.Raw)
        {
            string text =
                expression.Text;

            if (rawExpressions.TryGetValue(
                    text,
                    out int rawCount))
            {
                rawExpressions[text] =
                    rawCount + 1;
            }
            else
            {
                rawExpressions[text] = 1;
            }

            if (text == "0 , 0" ||
                text ==
                    "#13#10 '[%s]' #13#10#13#10")
            {
                Console.WriteLine();

                Console.WriteLine(
                    "================================");

                Console.WriteLine(
                    " RAW EXPRESSION DIAGNOSTIC");

                Console.WriteLine(
                    "================================");

                Console.WriteLine(
                    $"File:      {filePath}");

                Console.WriteLine(
                    $"Procedure: {procedureName}");

                Console.WriteLine(
                    $"Line:      {line}");

                Console.WriteLine(
                    $"Raw:       {text}");

                Console.WriteLine(
                    "================================");

                Console.WriteLine();
            }
        }

        foreach (
            ExpressionParser.ExpressionNode child
            in expression.Children)
        {
            CountExpression(
                child,
                counts,
                rawExpressions,
                ref totalNodes,
                filePath,
                procedureName,
                line);
        }
    }
}

