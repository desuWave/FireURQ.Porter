using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

public static class BehaviorAnalyzer
{
    public enum StatementKind
    {
        Block,
        Assignment,
        Call,
        If,
        While,
        For,
        Case,
        Repeat,
        Try,
        Raise,
        Exit,
        Break,
        Continue,
        Expression
    }

    public sealed record BehaviorClause(
        string Kind,
        string Header,
        IReadOnlyList<BehaviorStatement> Statements);

    public sealed record BehaviorStatement(
        StatementKind Kind,
        string Text,
        int Line,
        IReadOnlyList<BehaviorStatement> Children,
        IReadOnlyList<BehaviorStatement>? ElseChildren = null,
        string? SecondaryText = null,
        IReadOnlyList<BehaviorClause>? Clauses = null,
        ExpressionParser.AssignmentExpression? Assignment = null);

    public sealed record BehaviorInfo(
        string FilePath,
        string Name,
        IReadOnlyList<BehaviorStatement> Statements);

    public static BehaviorInfo Analyze(
        string filePath,
        string name,
        IReadOnlyList<LexerToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return new BehaviorInfo(
                filePath,
                name,
                Array.Empty<BehaviorStatement>());
        }

        var parser =
            new StatementParser(tokens);

        return new BehaviorInfo(
            filePath,
            name,
            parser.Parse());
    }

    public static void Print(
        BehaviorInfo behavior,
        List<string> lines)
    {
        AddLine(
            lines,
            $"BEHAVIOR: {behavior.Name}");

        AddLine(
            lines,
            $"FILE: {Path.GetFileName(behavior.FilePath)}");

        AddLine(lines);

        foreach (var statement in behavior.Statements)
        {
            PrintStatement(
                statement,
                lines,
                0);
        }

        AddLine(lines);
    }

    private static void PrintStatement(
        BehaviorStatement statement,
        List<string> lines,
        int depth)
    {
        string indent =
            new string(
                ' ',
                depth * 2);

        string kind =
            statement.Kind
                .ToString()
                .ToUpperInvariant();

        AddLine(
            lines,
            $"{indent}{kind}: {statement.Text}");

        if (statement.Assignment is not null)
        {
            string target =
                FormatExpression(
                    statement.Assignment.Target);

            string value =
                FormatExpression(
                    statement.Assignment.Value);

            AddLine(
                lines,
                $"{indent}  TARGET: {target}");

            AddLine(
                lines,
                $"{indent}  VALUE: {value}");
        }

        if (!string.IsNullOrWhiteSpace(
                statement.SecondaryText))
        {
            AddLine(
                lines,
                $"{indent}  {statement.SecondaryText}:");
        }

        foreach (var child in statement.Children)
        {
            PrintStatement(
                child,
                lines,
                depth + 1);
        }

        if (statement.ElseChildren is not null)
        {
            AddLine(
                lines,
                $"{indent}ELSE:");

            foreach (var child in statement.ElseChildren)
            {
                PrintStatement(
                    child,
                    lines,
                    depth + 1);
            }
        }

        if (statement.Clauses is not null)
        {
            foreach (var clause in statement.Clauses)
            {
                AddLine(
                    lines,
                    $"{indent}{clause.Kind}:");

                foreach (var child in clause.Statements)
                {
                    PrintStatement(
                        child,
                        lines,
                        depth + 1);
                }
            }
        }
    }

    private static void AddLine(
        List<string> lines,
        string text = "")
    {
        lines.Add(text);
    }

    private sealed class StatementParser
    {
        private readonly IReadOnlyList<LexerToken> _tokens;
        private int _position;

        public StatementParser(
            IReadOnlyList<LexerToken> tokens)
        {
            _tokens = tokens;
        }

        public IReadOnlyList<BehaviorStatement> Parse()
        {
            var result =
                new List<BehaviorStatement>();

            SkipBegin();

            while (!IsEnd())
            {
                if (PeekText("end"))
                {
                    Advance();
                    break;
                }

                if (PeekText(";"))
                {
                    Advance();
                    continue;
                }

                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    result.Add(statement);
                }
                else
                {
                    Advance();
                }
            }

            return result;
        }

        private BehaviorStatement? ParseStatement()
        {
            if (PeekText("begin"))
            {
                return ParseBlock();
            }

            if (PeekText("if"))
            {
                return ParseIf();
            }

            if (PeekText("while"))
            {
                return ParseWhile();
            }

            if (PeekText("for"))
            {
                return ParseFor();
            }

            if (PeekText("case"))
            {
                return ParseCase();
            }

            if (PeekText("repeat"))
            {
                return ParseRepeat();
            }

            if (PeekText("try"))
            {
                return ParseTry();
            }

            if (PeekText("raise"))
            {
                return ParseSimpleStatement(
                    StatementKind.Raise);
            }

            if (PeekText("exit"))
            {
                return ParseSimpleStatement(
                    StatementKind.Exit);
            }

            if (PeekText("break"))
            {
                return ParseSimpleStatement(
                    StatementKind.Break);
            }

            if (PeekText("continue"))
            {
                return ParseSimpleStatement(
                    StatementKind.Continue);
            }

            return ParseExpressionStatement();
        }

        private BehaviorStatement ParseBlock()
        {
            int line =
                Current().Line;

            Advance();

            var children =
                ParseStatementsUntil(
                    "end");

            if (PeekText("end"))
            {
                Advance();
            }

            MatchText(";");

            return new BehaviorStatement(
                StatementKind.Block,
                "begin ... end",
                line,
                children);
        }

        private BehaviorStatement ParseIf()
        {
            int line =
                Current().Line;

            Advance();

            var conditionTokens =
                ReadUntilKeyword("then");

            MatchText("then");

            string condition =
                JoinTokens(conditionTokens);

            var children =
                new List<BehaviorStatement>();

            if (!IsEnd() &&
                !PeekText("else") &&
                !PeekText("end"))
            {
                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    children.Add(statement);
                }
            }

            List<BehaviorStatement>? elseChildren =
                null;

            if (MatchText("else"))
            {
                elseChildren =
                    new List<BehaviorStatement>();

                if (!IsEnd() &&
                    !PeekText("end") &&
                    !PeekText("else"))
                {
                    BehaviorStatement? statement =
                        ParseStatement();

                    if (statement is not null)
                    {
                        elseChildren.Add(
                            statement);
                    }
                }
            }

            return new BehaviorStatement(
                StatementKind.If,
                condition,
                line,
                children,
                elseChildren);
        }

        private BehaviorStatement ParseWhile()
        {
            int line =
                Current().Line;

            Advance();

            var conditionTokens =
                ReadUntilKeyword("do");

            MatchText("do");

            string condition =
                JoinTokens(conditionTokens);

            var children =
                new List<BehaviorStatement>();

            if (!IsEnd())
            {
                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    children.Add(statement);
                }
            }

            return new BehaviorStatement(
                StatementKind.While,
                condition,
                line,
                children);
        }

        private BehaviorStatement ParseFor()
        {
            int line =
                Current().Line;

            Advance();

            var headerTokens =
                ReadUntilKeyword("do");

            MatchText("do");

            string header =
                JoinTokens(headerTokens);

            var children =
                new List<BehaviorStatement>();

            if (!IsEnd())
            {
                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    children.Add(statement);
                }
            }

            return new BehaviorStatement(
                StatementKind.For,
                header,
                line,
                children);
        }

        private BehaviorStatement ParseCase()
        {
            int line =
                Current().Line;

            Advance();

            var expressionTokens =
                new List<LexerToken>();

            int depth = 0;

            while (!IsEnd())
            {
                if (PeekText("("))
                {
                    depth++;
                }
                else if (PeekText(")"))
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                }

                if (depth == 0 &&
                    PeekText("of"))
                {
                    break;
                }

                expressionTokens.Add(
                    Current());

                Advance();
            }

            MatchText("of");

            string expression =
                JoinTokens(expressionTokens);

            var children =
                new List<BehaviorStatement>();

            while (!IsEnd() &&
                   !PeekText("end"))
            {
                if (PeekText(";"))
                {
                    Advance();
                    continue;
                }

                if (PeekText("else"))
                {
                    Advance();

                    var elseStatements =
                        ParseStatementsUntil(
                            "end");

                    children.Add(
                        new BehaviorStatement(
                            StatementKind.Expression,
                            "else",
                            CurrentLine(),
                            elseStatements));

                    break;
                }

                var labelTokens =
                    new List<LexerToken>();

                while (!IsEnd() &&
                       !PeekText(":") &&
                       !PeekText("end"))
                {
                    labelTokens.Add(
                        Current());

                    Advance();
                }

                if (MatchText(":"))
                {
                    string label =
                        JoinTokens(labelTokens);

                    BehaviorStatement? statement =
                        ParseStatement();

                    var armChildren =
                        new List<BehaviorStatement>();

                    if (statement is not null)
                    {
                        armChildren.Add(
                            statement);
                    }

                    children.Add(
                        new BehaviorStatement(
                            StatementKind.Expression,
                            $"case {label}",
                            labelTokens.Count > 0
                                ? labelTokens[0].Line
                                : line,
                            armChildren));
                }
                else
                {
                    break;
                }
            }

            if (PeekText("end"))
            {
                Advance();
            }

            MatchText(";");

            return new BehaviorStatement(
                StatementKind.Case,
                expression,
                line,
                children);
        }

        private BehaviorStatement ParseRepeat()
        {
            int line =
                Current().Line;

            Advance();

            var body =
                ParseStatementsUntil(
                    "until");

            MatchText("until");

            var conditionTokens =
                ReadUntilStatementEnd();

            string condition =
                JoinTokens(conditionTokens);

            MatchText(";");

            return new BehaviorStatement(
                StatementKind.Repeat,
                $"until {condition}",
                line,
                body);
        }

        private BehaviorStatement ParseTry()
        {
            int line =
                Current().Line;

            Advance();

            var tryChildren =
                ParseStatementsUntil(
                    "except",
                    "finally",
                    "end");

            var clauses =
                new List<BehaviorClause>();

            if (MatchText("except"))
            {
                var exceptChildren =
                    ParseExceptionHandlers();

                clauses.Add(
                    new BehaviorClause(
                        "EXCEPT",
                        "except",
                        exceptChildren));
            }

            if (MatchText("finally"))
            {
                var finallyChildren =
                    ParseStatementsUntil(
                        "end");

                clauses.Add(
                    new BehaviorClause(
                        "FINALLY",
                        "finally",
                        finallyChildren));
            }

            if (PeekText("end"))
            {
                Advance();
            }

            MatchText(";");

            return new BehaviorStatement(
                StatementKind.Try,
                "try",
                line,
                tryChildren,
                Clauses: clauses);
        }

        private List<BehaviorStatement>
            ParseExceptionHandlers()
        {
            var handlers =
                new List<BehaviorStatement>();

            while (!IsEnd() &&
                   !PeekText("finally") &&
                   !PeekText("end"))
            {
                if (PeekText(";"))
                {
                    Advance();
                    continue;
                }

                if (PeekText("on"))
                {
                    handlers.Add(
                        ParseExceptionHandler());

                    continue;
                }

                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    handlers.Add(statement);
                }
                else
                {
                    Advance();
                }
            }

            return handlers;
        }

        private BehaviorStatement
            ParseExceptionHandler()
        {
            int line =
                Current().Line;

            Advance();

            var headerTokens =
                ReadUntilKeyword("do");

            MatchText("do");

            string header =
                JoinTokens(headerTokens);

            var children =
                new List<BehaviorStatement>();

            if (!IsEnd())
            {
                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    children.Add(statement);
                }
            }

            return new BehaviorStatement(
                StatementKind.Expression,
                $"on {header}",
                line,
                children);
        }

        private BehaviorStatement?
            ParseExpressionStatement()
        {
            if (IsEnd() ||
                PeekText("end") ||
                PeekText("else") ||
                PeekText("until") ||
                PeekText("except") ||
                PeekText("finally") ||
                PeekText(";"))
            {
                return null;
            }

            var statementTokens =
                new List<LexerToken>();

            int roundDepth = 0;
            int squareDepth = 0;

            while (!IsEnd())
            {
                if (PeekText("("))
                {
                    roundDepth++;
                }
                else if (PeekText(")"))
                {
                    if (roundDepth > 0)
                    {
                        roundDepth--;
                    }
                }
                else if (PeekText("["))
                {
                    squareDepth++;
                }
                else if (PeekText("]"))
                {
                    if (squareDepth > 0)
                    {
                        squareDepth--;
                    }
                }

                if (PeekText(";") &&
                    roundDepth == 0 &&
                    squareDepth == 0)
                {
                    break;
                }

                if ((PeekText("end") ||
                     PeekText("else") ||
                     PeekText("until") ||
                     PeekText("except") ||
                     PeekText("finally")) &&
                    roundDepth == 0 &&
                    squareDepth == 0)
                {
                    break;
                }

                statementTokens.Add(
                    Current());

                Advance();
            }

            MatchText(";");

            if (statementTokens.Count == 0)
            {
                return null;
            }

            int line =
                statementTokens[0].Line;

            string text =
                JoinTokens(statementTokens);

            int assignmentIndex =
                FindTopLevelOperator(
                    statementTokens,
                    ":=");

            if (assignmentIndex >= 0)
            {
                var assignment =
                    ExpressionParser.ParseAssignment(
                        statementTokens);

                return new BehaviorStatement(
                    StatementKind.Assignment,
                    text,
                    line,
                    Array.Empty<BehaviorStatement>(),
                    Assignment: assignment);
            }

            if (LooksLikeCall(statementTokens))
            {
                return new BehaviorStatement(
                    StatementKind.Call,
                    text,
                    line,
                    Array.Empty<BehaviorStatement>());
            }

            return new BehaviorStatement(
                StatementKind.Expression,
                text,
                line,
                Array.Empty<BehaviorStatement>());
        }

        private BehaviorStatement ParseSimpleStatement(
            StatementKind kind)
        {
            var tokens =
                new List<LexerToken>();

            int line =
                Current().Line;

            while (!IsEnd() &&
                   !PeekText(";") &&
                   !PeekText("end") &&
                   !PeekText("until") &&
                   !PeekText("except") &&
                   !PeekText("finally"))
            {
                tokens.Add(
                    Current());

                Advance();
            }

            MatchText(";");

            return new BehaviorStatement(
                kind,
                JoinTokens(tokens),
                line,
                Array.Empty<BehaviorStatement>());
        }

        private List<BehaviorStatement>
            ParseStatementsUntil(
                params string[] terminators)
        {
            var result =
                new List<BehaviorStatement>();

            while (!IsEnd() &&
                   !IsAnyTerminator(terminators))
            {
                if (PeekText(";"))
                {
                    Advance();
                    continue;
                }

                BehaviorStatement? statement =
                    ParseStatement();

                if (statement is not null)
                {
                    result.Add(statement);
                }
                else
                {
                    Advance();
                }
            }

            return result;
        }

        private List<LexerToken>
            ReadUntilKeyword(
                string keyword)
        {
            var result =
                new List<LexerToken>();

            int roundDepth = 0;
            int squareDepth = 0;

            while (!IsEnd())
            {
                if (PeekText("("))
                {
                    roundDepth++;
                }
                else if (PeekText(")"))
                {
                    if (roundDepth > 0)
                    {
                        roundDepth--;
                    }
                }
                else if (PeekText("["))
                {
                    squareDepth++;
                }
                else if (PeekText("]"))
                {
                    if (squareDepth > 0)
                    {
                        squareDepth--;
                    }
                }

                if (roundDepth == 0 &&
                    squareDepth == 0 &&
                    PeekText(keyword))
                {
                    break;
                }

                result.Add(
                    Current());

                Advance();
            }

            return result;
        }

        private List<LexerToken>
            ReadUntilStatementEnd()
        {
            var result =
                new List<LexerToken>();

            int roundDepth = 0;
            int squareDepth = 0;

            while (!IsEnd())
            {
                if (PeekText("("))
                {
                    roundDepth++;
                }
                else if (PeekText(")"))
                {
                    if (roundDepth > 0)
                    {
                        roundDepth--;
                    }
                }
                else if (PeekText("["))
                {
                    squareDepth++;
                }
                else if (PeekText("]"))
                {
                    if (squareDepth > 0)
                    {
                        squareDepth--;
                    }
                }

                if (PeekText(";") &&
                    roundDepth == 0 &&
                    squareDepth == 0)
                {
                    break;
                }

                if (PeekText("end") &&
                    roundDepth == 0 &&
                    squareDepth == 0)
                {
                    break;
                }

                result.Add(
                    Current());

                Advance();
            }

            return result;
        }

        private static bool LooksLikeCall(
            IReadOnlyList<LexerToken> tokens)
        {
            if (tokens.Count < 2)
            {
                return false;
            }

            for (int i = 1;
                 i < tokens.Count;
                 i++)
            {
                if (tokens[i].Text == "(")
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindTopLevelOperator(
            IReadOnlyList<LexerToken> tokens,
            string text)
        {
            int roundDepth = 0;
            int squareDepth = 0;

            for (int i = 0;
                 i < tokens.Count;
                 i++)
            {
                string current =
                    tokens[i].Text;

                if (current == "(")
                {
                    roundDepth++;
                    continue;
                }

                if (current == ")")
                {
                    if (roundDepth > 0)
                    {
                        roundDepth--;
                    }

                    continue;
                }

                if (current == "[")
                {
                    squareDepth++;
                    continue;
                }

                if (current == "]")
                {
                    if (squareDepth > 0)
                    {
                        squareDepth--;
                    }

                    continue;
                }

                if (roundDepth == 0 &&
                    squareDepth == 0 &&
                    current == text)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SkipBegin()
        {
            if (PeekText("begin"))
            {
                Advance();
            }
        }

        private bool IsAnyTerminator(
            IReadOnlyList<string> terminators)
        {
            foreach (string terminator in terminators)
            {
                if (PeekText(terminator))
                {
                    return true;
                }
            }

            return false;
        }

        private bool PeekText(string text)
        {
            return !IsEnd() &&
                   string.Equals(
                       Current().Text,
                       text,
                       StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchText(string text)
        {
            if (!PeekText(text))
            {
                return false;
            }

            Advance();
            return true;
        }

        private int CurrentLine()
        {
            return IsEnd()
                ? 0
                : Current().Line;
        }

        private LexerToken Current()
        {
            return _tokens[_position];
        }

        private void Advance()
        {
            if (_position < _tokens.Count)
            {
                _position++;
            }
        }

        private bool IsEnd()
        {
            return _position >= _tokens.Count;
        }

        private static string JoinTokens(
            IReadOnlyList<LexerToken> tokens)
        {
            return string.Join(
                " ",
                tokens.Select(x => x.Text));
        }
    }

    private static string FormatExpression(
    ExpressionParser.ExpressionNode expression)
    {
        return expression.Kind switch
        {
            ExpressionParser.ExpressionKind.Identifier =>
                $"IDENTIFIER {expression.Text}",

            ExpressionParser.ExpressionKind.Literal =>
                $"LITERAL {expression.Text}",

            ExpressionParser.ExpressionKind.MemberAccess =>
                FormatMemberAccess(expression),

            ExpressionParser.ExpressionKind.IndexAccess =>
                FormatIndexAccess(expression),

            ExpressionParser.ExpressionKind.Call =>
                FormatCall(expression),

            ExpressionParser.ExpressionKind.Unary =>
                FormatUnary(expression),

            ExpressionParser.ExpressionKind.Binary =>
                FormatBinary(expression),

            ExpressionParser.ExpressionKind.Raw =>
                $"RAW {expression.Text}",

            _ =>
                expression.Text
        };
    }

    private static string FormatMemberAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count != 2)
        {
            return $"MEMBER {expression.Text}";
        }

        return
            $"MEMBER " +
            $"{FormatExpression(expression.Children[0])}." +
            $"{FormatExpression(expression.Children[1])}";
    }

    private static string FormatIndexAccess(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count != 2)
        {
            return "INDEX";
        }

        return
            $"INDEX " +
            $"{FormatExpression(expression.Children[0])}" +
            $"[{FormatExpression(expression.Children[1])}]";
    }

    private static string FormatCall(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count == 0)
        {
            return "CALL";
        }

        string target =
            FormatExpression(
                expression.Children[0]);

        if (expression.Children.Count == 1)
        {
            return $"CALL {target}()";
        }

        var arguments =
            expression.Children
                .Skip(1)
                .Select(FormatExpression);

        return
            $"CALL {target}(" +
            string.Join(", ", arguments) +
            ")";
    }

    private static string FormatUnary(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count == 0)
        {
            return expression.Text;
        }

        return
            $"{expression.Text} " +
            FormatExpression(
                expression.Children[0]);
    }

    private static string FormatBinary(
        ExpressionParser.ExpressionNode expression)
    {
        if (expression.Children.Count != 2)
        {
            return expression.Text;
        }

        return
            $"{FormatExpression(expression.Children[0])} " +
            $"{expression.Text} " +
            $"{FormatExpression(expression.Children[1])}";
    }
}
