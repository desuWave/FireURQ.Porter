using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public sealed class BodyIndex
{
    public sealed record BodyEntry(
        string FilePath,
        string Name,
        bool IsProcedure,
        int TokenCount,
        BodyTokenAnalyzer.BodyOwnerKind OwnerKind,
        BehaviorAnalyzer.BehaviorInfo Behavior,
        int Assignments,
        int Calls,
        int Conditions,
        int Loops,
        int Expressions,
        IReadOnlyList<LexerToken> BodyTokens);

    private readonly List<BodyEntry> _bodies = new();

    public IReadOnlyList<BodyEntry> Bodies =>
        _bodies;

    public static BodyIndex Build(
        string rootPath)
    {
        var index =
            new BodyIndex();

        string[] files =
            Directory.GetFiles(
                rootPath,
                "*.pas",
                SearchOption.AllDirectories);

        foreach (string filePath in files)
        {
            try
            {
                string source =
                    File.ReadAllText(
                        filePath,
                        System.Text.Encoding.GetEncoding(1251));

                var lexer =
                    new DelphiLexer(source);

                IReadOnlyList<LexerToken> tokens =
                    lexer.Tokenize();

                var parser =
                    new DelphiParser(tokens);

                DelphiUnit unit =
                    parser.ParseUnit();

                IReadOnlyList<BodyTokenAnalyzer.BodyInfo> bodies =
                    BodyTokenAnalyzer.Analyze(
                        unit,
                        filePath);

                foreach (
                    BodyTokenAnalyzer.BodyInfo body
                    in bodies)
                {
                    if (body.BodyTokens.Count == 0)
                        continue;

                    BehaviorAnalyzer.BehaviorInfo behavior =
                        BehaviorAnalyzer.Analyze(
                            body.FilePath,
                            body.Name,
                            body.BodyTokens);

                    BehaviorMetricsAnalyzer.Metrics metrics =
                        BehaviorMetricsAnalyzer.Analyze(
                            behavior);

                    index._bodies.Add(
                        new BodyEntry(
                            body.FilePath,
                            body.Name,
                            body.IsProcedure,
                            body.TokenCount,
                            body.OwnerKind,
                            behavior,
                            metrics.Assignments,
                            metrics.Calls,
                            metrics.Conditions,
                            metrics.Loops,
                            metrics.Expressions,
                            body.BodyTokens));
                }
            }
            catch
            {
                // Один повреждённый или пока
                // неподдерживаемый файл не должен
                // ломать построение всего индекса.
            }
        }

        return index;
    }

    public IReadOnlyList<BodyEntry> Search(
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Bodies;

        return _bodies
            .Where(
                body =>
                    body.Name.Contains(
                        query,
                        StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(body.FilePath)
                        .Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public BodyEntry? FindExact(
        string name)
    {
        return _bodies.FirstOrDefault(
            body =>
                body.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void CountStatements(
        IReadOnlyList<BehaviorAnalyzer.BehaviorStatement> statements,
        out int assignments,
        out int calls,
        out int conditions,
        out int loops,
        out int expressions)
    {
        assignments = 0;
        calls = 0;
        conditions = 0;
        loops = 0;
        expressions = 0;

        foreach (
            BehaviorAnalyzer.BehaviorStatement statement
            in statements)
        {
            CountStatement(
                statement,
                ref assignments,
                ref calls,
                ref conditions,
                ref loops,
                ref expressions);
        }
    }

    private static void CountStatement(
        BehaviorAnalyzer.BehaviorStatement statement,
        ref int assignments,
        ref int calls,
        ref int conditions,
        ref int loops,
        ref int expressions)
    {
        switch (statement.Kind)
        {
            case BehaviorAnalyzer.StatementKind.Assignment:
                assignments++;
                break;

            case BehaviorAnalyzer.StatementKind.Call:
                calls++;
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
                expressions++;
                break;
        }

        foreach (
            BehaviorAnalyzer.BehaviorStatement child
            in statement.Children)
        {
            CountStatement(
                child,
                ref assignments,
                ref calls,
                ref conditions,
                ref loops,
                ref expressions);
        }

        if (statement.ElseChildren != null)
        {
            foreach (
                BehaviorAnalyzer.BehaviorStatement child
                in statement.ElseChildren)
            {
                CountStatement(
                    child,
                    ref assignments,
                    ref calls,
                    ref conditions,
                    ref loops,
                    ref expressions);
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
                    CountStatement(
                        child,
                        ref assignments,
                        ref calls,
                        ref conditions,
                        ref loops,
                        ref expressions);
                }
            }
        }
    }
}