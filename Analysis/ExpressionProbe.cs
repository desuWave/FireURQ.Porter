using FireURQ.Porter.AST;
using FireURQ.Porter.Lexer;
using FireURQ.Porter.Parser;

namespace FireURQ.Porter.Analysis;

public static class ExpressionProbe
{
    public static void Analyze(
        string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            Console.WriteLine(
                $"ERROR: Directory not found: {rootPath}");

            return;
        }

        var files =
            Directory
                .EnumerateFiles(
                    rootPath,
                    "*.pas",
                    SearchOption.AllDirectories)
                .OrderBy(x => x)
                .ToList();

        int parsedFiles = 0;
        int failedFiles = 0;
        int assignments = 0;

        var lines =
            new List<string>();

        AddLine(
            lines,
            "================================");

        AddLine(
            lines,
            " Expression Probe");

        AddLine(
            lines,
            "================================");

        AddLine(lines);

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

                parsedFiles++;

                var bodies =
                    BodyTokenAnalyzer.Analyze(
                        unit,
                        filePath);

                foreach (var body in bodies)
                {
                    if (body.BodyTokens.Count == 0)
                    {
                        continue;
                    }

                    var behavior =
                        BehaviorAnalyzer.Analyze(
                            filePath,
                            body.Name,
                            body.BodyTokens);

                    foreach (var statement in
                             behavior.Statements)
                    {
                        assignments +=
                            ProbeStatements(
                                body.Name,
                                statement,
                                lines);
                    }
                }
            }
            catch (Exception ex)
            {
                failedFiles++;

                AddLine(
                    lines,
                    $"[PROBE-ERROR] " +
                    $"{Path.GetFileName(filePath)}");

                AddLine(
                    lines,
                    $"  {ex.Message}");

                AddLine(lines);
            }
        }

        AddLine(
            lines,
            "================================");

        AddLine(
            lines,
            " Expression Probe Result");

        AddLine(
            lines,
            "================================");

        AddLine(
            lines,
            $"Files found:       {files.Count}");

        AddLine(
            lines,
            $"Parsed:             {parsedFiles}");

        AddLine(
            lines,
            $"Failed:             {failedFiles}");

        AddLine(
            lines,
            $"Assignments probed: {assignments}");

        AddLine(
            lines,
            "================================");

        var listing =
            new PagedListing(
                lines,
                pageSize: Math.Max(
                    1,
                    Console.WindowHeight - 7));

        PagedListingViewer.Show(
            listing,
            "FireURQ.Porter — Expression Probe");
    }

    private static int ProbeStatements(
        string bodyName,
        BehaviorAnalyzer.BehaviorStatement statement,
        List<string> lines)
    {
        int count = 0;

        if (statement.Assignment is not null)
        {
            count++;

            AddLine(
                lines,
                "--------------------------------");

            AddLine(
                lines,
                $"BODY: {bodyName}");

            AddLine(
                lines,
                $"LINE: {statement.Line}");

            AddLine(
                lines,
                $"SOURCE: {statement.Text}");

            AddLine(lines);

            AddLine(
                lines,
                "TARGET:");

            PrintExpression(
                statement.Assignment.Target,
                lines,
                1);

            AddLine(lines);

            AddLine(
                lines,
                "VALUE:");

            PrintExpression(
                statement.Assignment.Value,
                lines,
                1);

            AddLine(lines);
        }

        foreach (var child in
                 statement.Children)
        {
            count +=
                ProbeStatements(
                    bodyName,
                    child,
                    lines);
        }

        if (statement.ElseChildren is not null)
        {
            foreach (var child in
                     statement.ElseChildren)
            {
                count +=
                    ProbeStatements(
                        bodyName,
                        child,
                        lines);
            }
        }

        if (statement.Clauses is not null)
        {
            foreach (var clause in
                     statement.Clauses)
            {
                foreach (var child in
                         clause.Statements)
                {
                    count +=
                        ProbeStatements(
                            bodyName,
                            child,
                            lines);
                }
            }
        }

        return count;
    }

    private static void PrintExpression(
        ExpressionParser.ExpressionNode expression,
        List<string> lines,
        int depth)
    {
        string indent =
            new string(
                ' ',
                depth * 2);

        AddLine(
            lines,
            $"{indent}{expression.Kind}: " +
            $"{expression.Text}");

        foreach (var child in
                 expression.Children)
        {
            PrintExpression(
                child,
                lines,
                depth + 1);
        }
    }

    private static void AddLine(
        List<string> lines,
        string text = "")
    {
        lines.Add(text);
    }
}