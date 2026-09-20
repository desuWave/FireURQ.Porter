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

    public enum SemanticReferenceKind
    {
        Read,
        Write,
        Call,
        MemberAccess,
        IndexAccess
    }

    public sealed record SemanticReference(
        SemanticReferenceKind Kind,
        string Text,
        string FilePath,
        string ProcedureName,
        int Line);

    public sealed record SemanticCounts(
        IReadOnlyDictionary<
            SemanticReferenceKind,
            int> ByKind,
        IReadOnlyList<SemanticReference> References);

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
                    ref statementsWithExpressions);
            }
        }

        return new Counts(
            totalNodes,
            statementsWithExpressions,
            counts,
            rawExpressions);
    }

    public static SemanticCounts AnalyzeSemantics(
        IReadOnlyList<BehaviorAnalyzer.BehaviorInfo> behaviors)
    {
        var semanticReferences =
            new Dictionary<
                SemanticReferenceKind,
                int>();

        var references =
            new List<SemanticReference>();

        foreach (
            BehaviorAnalyzer.BehaviorInfo behavior
            in behaviors)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement statement
                in behavior.Statements)
            {
                AnalyzeSemanticStatement(
                    statement,
                    semanticReferences,
                    references,
                    behavior.FilePath,
                    behavior.Name);
            }
        }

        return new SemanticCounts(
            semanticReferences,
            references);
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

    public static void PrintSemantic(
        SemanticCounts counts)
    {
        Console.WriteLine();
        Console.WriteLine(
            "================================");
        Console.WriteLine(
            " Semantic Census");
        Console.WriteLine(
            "================================");
        Console.WriteLine();

        foreach (
            SemanticReferenceKind kind
            in Enum.GetValues<
                SemanticReferenceKind>())
        {
            counts.ByKind.TryGetValue(
                kind,
                out int count);

            Console.WriteLine(
                $"{kind,-16} {count,6}");
        }

        Console.WriteLine();

        Console.WriteLine(
            "Semantic References");

        Console.WriteLine(
            "--------------------------------");

        foreach (
            SemanticReference reference
            in counts.References.Take(20))
        {
            Console.WriteLine(
                $"{reference.Kind,-16} " +
                $"{reference.Text,-40} " +
                $"{reference.FilePath}:{reference.Line} " +
                $"[{reference.ProcedureName}]");
        }

        Console.WriteLine();

        Console.WriteLine(
            $"Total semantic references: " +
            $"{counts.References.Count}");

        Console.WriteLine();
    }

    private static void AnalyzeStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        Dictionary<
            ExpressionParser.ExpressionKind,
            int> counts,
        Dictionary<string, int> rawExpressions,
        ref int totalNodes,
        ref int statementsWithExpressions)
    {
        bool hasExpression = false;

        if (statement.Expression is not null)
        {
            hasExpression = true;

            CountExpression(
                statement.Expression,
                counts,
                rawExpressions,
                ref totalNodes);
        }

        if (statement.Assignment is not null)
        {
            hasExpression = true;

            CountExpression(
                statement.Assignment.Target,
                counts,
                rawExpressions,
                ref totalNodes);

            CountExpression(
                statement.Assignment.Value,
                counts,
                rawExpressions,
                ref totalNodes);
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
                ref statementsWithExpressions);
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
                    ref statementsWithExpressions);
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
                        ref statementsWithExpressions);
                }
            }
        }
    }

    private static void AnalyzeSemanticStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        Dictionary<
            SemanticReferenceKind,
            int> semanticReferences,
        List<SemanticReference> references,
        string filePath,
        string procedureName)
    {
        if (statement.Expression is not null)
        {
            CollectSemanticReferences(
                statement.Expression,
                semanticReferences,
                references,
                filePath,
                procedureName,
                statement.Line);
        }

        if (statement.Assignment is not null)
        {
            CollectSemanticReferences(
                statement.Assignment.Target,
                semanticReferences,
                references,
                filePath,
                procedureName,
                statement.Line,
                isWriteTarget: true);

            CollectSemanticReferences(
                statement.Assignment.Value,
                semanticReferences,
                references,
                filePath,
                procedureName,
                statement.Line);
        }

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            AnalyzeSemanticStatement(
                child,
                semanticReferences,
                references,
                filePath,
                procedureName);
        }

        if (statement.ElseChildren is not null)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in statement.ElseChildren)
            {
                AnalyzeSemanticStatement(
                    child,
                    semanticReferences,
                    references,
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
                    AnalyzeSemanticStatement(
                        child,
                        semanticReferences,
                        references,
                        filePath,
                        procedureName);
                }
            }
        }
    }

    private static void CollectSemanticReferences(
        ExpressionParser.ExpressionNode expression,
        Dictionary<
            SemanticReferenceKind,
            int> semanticReferences,
        List<SemanticReference> references,
        string filePath,
        string procedureName,
        int line,
        bool isWriteTarget = false)
    {
        switch (expression.Kind)
        {
            case ExpressionParser.ExpressionKind.Identifier:
                {
                    AddSemanticReference(
                        semanticReferences,
                        references,
                        isWriteTarget
                            ? SemanticReferenceKind.Write
                            : SemanticReferenceKind.Read,
                        expression.Text,
                        filePath,
                        procedureName,
                        line);

                    break;
                }

            case ExpressionParser.ExpressionKind.MemberAccess:
                {
                    AddSemanticReference(
                        semanticReferences,
                        references,
                        SemanticReferenceKind.MemberAccess,
                        FormatExpression(expression),
                        filePath,
                        procedureName,
                        line);

                    if (expression.Children.Count > 0)
                    {
                        CollectSemanticReferences(
                            expression.Children[0],
                            semanticReferences,
                            references,
                            filePath,
                            procedureName,
                            line,
                            isWriteTarget);
                    }

                    break;
                }

            case ExpressionParser.ExpressionKind.IndexAccess:
                {
                    AddSemanticReference(
                        semanticReferences,
                        references,
                        SemanticReferenceKind.IndexAccess,
                        FormatExpression(expression),
                        filePath,
                        procedureName,
                        line);

                    if (expression.Children.Count > 0)
                    {
                        CollectSemanticReferences(
                            expression.Children[0],
                            semanticReferences,
                            references,
                            filePath,
                            procedureName,
                            line,
                            isWriteTarget);
                    }

                    foreach (
                        ExpressionParser.ExpressionNode index
                        in expression.Children.Skip(1))
                    {
                        CollectSemanticReferences(
                            index,
                            semanticReferences,
                            references,
                            filePath,
                            procedureName,
                            line);
                    }

                    break;
                }

            case ExpressionParser.ExpressionKind.Call:
                {
                    string targetText =
                        expression.Children.Count >= 1
                            ? FormatExpression(expression.Children[0])
                            : expression.Text;

                    AddSemanticReference(
                        semanticReferences,
                        references,
                        SemanticReferenceKind.Call,
                        targetText,
                        filePath,
                        procedureName,
                        line);

                    foreach (ExpressionParser.ExpressionNode child in expression.Children)
                    {
                        CollectSemanticReferences(
                            child,
                            semanticReferences,
                            references,
                            filePath,
                            procedureName,
                            line);
                    }

                    break;
                }

            default:
                {
                    foreach (
                        ExpressionParser.ExpressionNode child
                        in expression.Children)
                    {
                        CollectSemanticReferences(
                            child,
                            semanticReferences,
                            references,
                            filePath,
                            procedureName,
                            line);
                    }

                    break;
                }
        }
    }

    private static void AddSemanticReference(
        Dictionary<
            SemanticReferenceKind,
            int> semanticReferences,
        List<SemanticReference> references,
        SemanticReferenceKind kind,
        string text,
        string filePath,
        string procedureName,
        int line)
    {
        if (semanticReferences.TryGetValue(
                kind,
                out int count))
        {
            semanticReferences[kind] =
                count + 1;
        }
        else
        {
            semanticReferences[kind] = 1;
        }

        references.Add(
            new SemanticReference(
                kind,
                text,
                filePath,
                procedureName,
                line));
    }

    private static string FormatExpression(
        ExpressionParser.ExpressionNode expression)
    {
        return expression.Kind switch
        {
            ExpressionParser.ExpressionKind.Empty =>
                string.Empty,

            ExpressionParser.ExpressionKind.Identifier =>
                expression.Text,

            ExpressionParser.ExpressionKind.Literal =>
                expression.Text,

            ExpressionParser.ExpressionKind.MemberAccess =>
                expression.Children.Count >= 2
                    ? $"{FormatExpression(expression.Children[0])}." +
                      $"{FormatExpression(expression.Children[1])}"
                    : expression.Text,

            ExpressionParser.ExpressionKind.IndexAccess =>
                expression.Children.Count >= 1
                    ? $"{FormatExpression(expression.Children[0])}" +
                      $"[" +
                      $"{string.Join(
                          ", ",
                          expression.Children
                              .Skip(1)
                              .Select(FormatExpression))}" +
                      $"]"
                    : expression.Text,

            ExpressionParser.ExpressionKind.Call =>
                expression.Children.Count >= 1
                    ? $"{FormatExpression(expression.Children[0])}" +
                      $"(" +
                      $"{string.Join(
                          ", ",
                          expression.Children
                              .Skip(1)
                              .Select(FormatExpression))}" +
                      $")"
                    : expression.Text,

            ExpressionParser.ExpressionKind.Unary =>
                expression.Children.Count >= 1
                    ? $"{expression.Text}" +
                      $"{FormatExpression(expression.Children[0])}"
                    : expression.Text,

            ExpressionParser.ExpressionKind.Binary =>
                expression.Children.Count >= 2
                    ? $"{FormatExpression(expression.Children[0])} " +
                      $"{expression.Text} " +
                      $"{FormatExpression(expression.Children[1])}"
                    : expression.Text,

            ExpressionParser.ExpressionKind.AddressOf =>
                expression.Children.Count >= 1
                    ? $"@{FormatExpression(expression.Children[0])}"
                    : expression.Text,

            ExpressionParser.ExpressionKind.Dereference =>
                expression.Children.Count >= 1
                    ? $"{FormatExpression(expression.Children[0])}^"
                    : expression.Text,

            ExpressionParser.ExpressionKind.TypeTest =>
                expression.Children.Count >= 2
                    ? $"{FormatExpression(expression.Children[0])} " +
                      $"{expression.Text} " +
                      $"{FormatExpression(expression.Children[1])}"
                    : expression.Text,

            ExpressionParser.ExpressionKind.TypeCast =>
                expression.Children.Count >= 1
                    ? $"{expression.Text}(" +
                      $"{FormatExpression(expression.Children[0])})"
                    : expression.Text,

            ExpressionParser.ExpressionKind.Set =>
                expression.Text,

            ExpressionParser.ExpressionKind.Inherited =>
                expression.Text,

            ExpressionParser.ExpressionKind.Raw =>
                expression.Text,

            _ =>
                expression.Text
        };
    }

    private static void CountExpression(
        ExpressionParser.ExpressionNode expression,
        Dictionary<
            ExpressionParser.ExpressionKind,
            int> counts,
        Dictionary<string, int> rawExpressions,
        ref int totalNodes)
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
        }

        foreach (
            ExpressionParser.ExpressionNode child
            in expression.Children)
        {
            CountExpression(
                child,
                counts,
                rawExpressions,
                ref totalNodes);
        }
    }
}