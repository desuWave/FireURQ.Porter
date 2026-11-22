using FireURQ.Porter.Lexer;

namespace FireURQ.Porter.Analysis;

public static class ExpressionParser
{
    public enum ExpressionKind
    {
        Empty,
        Identifier,
        Literal,
        MemberAccess,
        IndexAccess,
        Call,
        Unary,
        Binary,
        AddressOf,
        Dereference,
        TypeTest,
        TypeCast,
        Set,
        Range,
        Inherited,
        Raw
    }

    public sealed record ExpressionNode(
        ExpressionKind Kind,
        string Text,
        IReadOnlyList<ExpressionNode> Children);

    public sealed record AssignmentExpression(
        ExpressionNode Target,
        ExpressionNode Value);

    public static AssignmentExpression? ParseAssignment(
        IReadOnlyList<LexerToken> tokens)
    {
        int index =
            FindTopLevelOperator(
                tokens,
                ":=");

        if (index < 0)
        {
            return null;
        }

        var targetTokens =
            tokens
                .Take(index)
                .ToList();

        var valueTokens =
            tokens
                .Skip(index + 1)
                .ToList();

        return new AssignmentExpression(
            Parse(targetTokens),
            Parse(valueTokens));
    }

    public static ExpressionNode Parse(
        IReadOnlyList<LexerToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return EmptyNode();
        }

        return ParseRange(
            tokens,
            0,
            tokens.Count);
    }

    private static ExpressionNode ParseRange(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        if (start >= end)
        {
            return EmptyNode();
        }

        // Убираем только действительно внешние скобки.
        while (start < end &&
               tokens[start].Text == "(" &&
               tokens[end - 1].Text == ")")
        {
            if (!IsMatchingOuterParentheses(
                    tokens,
                    start,
                    end))
            {
                break;
            }

            start++;
            end--;

            if (start >= end)
            {
                return EmptyNode();
            }
        }

        // Delphi inherited call:
        //
        // inherited Foo
        // inherited Foo(X)
        // inherited Foo(A, B)
        //
        // Представляем как отдельный ExpressionKind.Inherited.
        // Text содержит имя вызываемого метода,
        // Children содержат аргументы.
        if (string.Equals(
                tokens[start].Text,
                "inherited",
                StringComparison.OrdinalIgnoreCase))
        {
            int methodIndex =
                start + 1;

            if (methodIndex >= end)
            {
                return new ExpressionNode(
                    ExpressionKind.Inherited,
                    "inherited",
                    Array.Empty<ExpressionNode>());
            }

            LexerToken methodToken =
                tokens[methodIndex];

            string method =
                methodToken.Text;

            int position =
                methodIndex + 1;

            var arguments =
                new List<ExpressionNode>();

            if (position < end &&
                tokens[position].Text == "(")
            {
                int closeParen =
                    FindMatchingParenthesis(
                        tokens,
                        position,
                        end);

                if (closeParen >= 0)
                {
                    arguments =
                        ParseArguments(
                            tokens,
                            position + 1,
                            closeParen);

                    position =
                        closeParen + 1;
                }
            }

            // Если после inherited-вызова остался
            // неожиданный хвост, сохраняем его отдельно
            // через Raw-узел, чтобы ничего не потерять.
            if (position < end)
            {
                var remaining =
                    ParseRange(
                        tokens,
                        position,
                        end);

                arguments.Add(
                    remaining);
            }

            return new ExpressionNode(
                ExpressionKind.Inherited,
                method,
                arguments);
        }

        // Binary operators.
        int binaryIndex =
            FindTopLevelBinaryOperator(
                tokens,
                start,
                end);

        if (binaryIndex >= 0)
        {
            var left =
                ParseRange(
                    tokens,
                    start,
                    binaryIndex);

            var right =
                ParseRange(
                    tokens,
                    binaryIndex + 1,
                    end);

            string operatorText =
                tokens[binaryIndex].Text;

            ExpressionKind kind =
                operatorText.Equals(
                    "is",
                    StringComparison.OrdinalIgnoreCase)
                    ? ExpressionKind.TypeTest
                    : operatorText.Equals(
                        "as",
                        StringComparison.OrdinalIgnoreCase)
                        ? ExpressionKind.TypeCast
                        : ExpressionKind.Binary;

            return new ExpressionNode(
                kind,
                operatorText,
                new[]
                {
                    left,
                    right
                });
        }

        // Prefix unary operators.
        if (IsUnaryOperator(
                tokens[start].Text))
        {
            var operand =
                ParseRange(
                    tokens,
                    start + 1,
                    end);

            return new ExpressionNode(
                ExpressionKind.Unary,
                tokens[start].Text,
                new[]
                {
                    operand
                });
        }

        // Pascal address-of operator.
        if (tokens[start].Text == "@")
        {
            var operand =
                ParseRange(
                    tokens,
                    start + 1,
                    end);

            return new ExpressionNode(
                ExpressionKind.AddressOf,
                "@",
                new[]
                {
                    operand
                });
        }

        // Postfix expression chain:
        //
        // A.B
        // A.B.C
        // A[B]
        // A[B].C
        // A(B)
        // A.B(X)
        // A.B(X).C
        // A[B](X)
        // A^
        // A[B]^
        //
        // Разбираем базовое выражение, а затем
        // последовательно навешиваем postfix-операции.
        ExpressionNode? postfix =
            ParsePostfix(
                tokens,
                start,
                end);

        if (postfix is not null)
        {
            return postfix;
        }

        // Single identifier / keyword / literal.
        if (end - start == 1)
        {
            LexerToken token =
                tokens[start];

            if (token.Kind == TokenKind.Identifier ||
                token.Kind == TokenKind.Keyword)
            {
                return new ExpressionNode(
                    ExpressionKind.Identifier,
                    token.Text,
                    Array.Empty<ExpressionNode>());
            }

            if (token.Kind == TokenKind.Number ||
                token.Kind == TokenKind.String ||
                token.Kind == TokenKind.Character)
            {
                return new ExpressionNode(
                    ExpressionKind.Literal,
                    token.Text,
                    Array.Empty<ExpressionNode>());
            }
        }

        // Неразобранное выражение сохраняем целиком.
        return new ExpressionNode(
            ExpressionKind.Raw,
            JoinTokens(
                tokens,
                start,
                end),
            Array.Empty<ExpressionNode>());
    }

    private static ExpressionNode? ParsePostfix(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        if (start >= end)
        {
            return null;
        }

        int position =
            start;

        ExpressionNode? expression =
            ParsePrimary(
                tokens,
                ref position,
                end);

        if (expression is null)
        {
            return null;
        }

        while (position < end)
        {
            // Member access:
            //
            // A.B
            // A.B.C
            // A().B
            // A[B].C
            if (tokens[position].Text == ".")
            {
                if (position + 1 >= end)
                {
                    return null;
                }

                position++;

                ExpressionNode? member =
                    ParseIdentifierOrLiteral(
                        tokens,
                        ref position,
                        end);

                if (member is null)
                {
                    return null;
                }

                expression =
                    new ExpressionNode(
                        ExpressionKind.MemberAccess,
                        ".",
                        new[]
                        {
                            expression,
                            member
                        });

                continue;
            }

            // Index access:
            //
            // A[B]
            // A[B].C
            // A[B](X)
            if (tokens[position].Text == "[")
            {
                int closeBracket =
                    FindMatchingBracket(
                        tokens,
                        position,
                        end);

                if (closeBracket < 0)
                {
                    return null;
                }

                var indices =
                    ParseArguments(
                        tokens,
                        position + 1,
                        closeBracket);

                expression =
                    new ExpressionNode(
                        ExpressionKind.IndexAccess,
                        "[]",
                        new[]
                        {
                expression
                        }
                        .Concat(indices)
                        .ToList());

                position =
                    closeBracket + 1;

                continue;
            }

            // Call:
            //
            // A()
            // A(X)
            // A.B(X)
            // A[B](X)
            if (tokens[position].Text == "(")
            {
                int closeParen =
                    FindMatchingParenthesis(
                        tokens,
                        position,
                        end);

                if (closeParen < 0)
                {
                    return null;
                }

                var arguments =
                    ParseArguments(
                        tokens,
                        position + 1,
                        closeParen);

                expression =
                    new ExpressionNode(
                        ExpressionKind.Call,
                        "call",
                        new[]
                        {
                            expression
                        }
                        .Concat(arguments)
                        .ToList());

                position =
                    closeParen + 1;

                continue;
            }

            // Pascal dereference operator:
            //
            // A^
            // A.B^
            // A[B]^
            if (tokens[position].Text == "^")
            {
                expression =
                    new ExpressionNode(
                        ExpressionKind.Dereference,
                        "^",
                        new[]
                        {
                            expression
                        });

                position++;

                continue;
            }

            return null;
        }

        return expression;
    }

    private static ExpressionNode? ParsePrimary(
        IReadOnlyList<LexerToken> tokens,
        ref int position,
        int end)
    {
        if (position >= end)
        {
            return null;
        }

        LexerToken token =
            tokens[position];

        if (token.Kind == TokenKind.Identifier ||
            token.Kind == TokenKind.Keyword)
        {
            position++;

            return new ExpressionNode(
                ExpressionKind.Identifier,
                token.Text,
                Array.Empty<ExpressionNode>());
        }

        // In primary position ^M is a control character. Postfix ^ stays a dereference.
        static bool IsControlLetter(IReadOnlyList<LexerToken> source, int at, int limit) =>
            at + 1 < limit && source[at].Text == "^" && source[at + 1].Text.Length == 1 &&
            char.ToUpperInvariant(source[at + 1].Text[0]) is >= 'A' and <= 'Z';

        if (token.Kind is TokenKind.Character or TokenKind.String || IsControlLetter(tokens, position, end))
        {
            var literal = new System.Text.StringBuilder();
            while (position < end)
            {
                if (tokens[position].Kind is TokenKind.String or TokenKind.Character)
                    literal.Append(tokens[position++].Text);
                else if (IsControlLetter(tokens, position, end))
                {
                    int code = char.ToUpperInvariant(tokens[position + 1].Text[0]) - 'A' + 1;
                    literal.Append('#').Append(code);
                    position += 2;
                }
                else break;
            }
            return new ExpressionNode(ExpressionKind.Literal, literal.ToString(), Array.Empty<ExpressionNode>());
        }

        if (token.Kind == TokenKind.Number ||
            token.Kind == TokenKind.String ||
            token.Kind == TokenKind.Character)
        {
            position++;

            return new ExpressionNode(
                ExpressionKind.Literal,
                token.Text,
                Array.Empty<ExpressionNode>());
        }

        if (token.Text == "(")
        {
            int closeParen =
                FindMatchingParenthesis(
                    tokens,
                    position,
                    end);

            if (closeParen < 0)
            {
                return null;
            }

            var inner =
                ParseRange(
                    tokens,
                    position + 1,
                    closeParen);

            position =
                closeParen + 1;

            return inner;
        }

        // Pascal set constructor:
        //
        // []
        // [A]
        // [A, B]
        // [A, B, C]
        //
        // Здесь "[" является началом выражения.
        // В отличие от A[B], который обрабатывается
        // в ParsePostfix как IndexAccess.
        if (token.Text == "[")
        {
            int closeBracket =
                FindMatchingBracket(
                    tokens,
                    position,
                    end);

            if (closeBracket < 0)
            {
                return null;
            }

            var elements =
                ParseArguments(
                    tokens,
                    position + 1,
                    closeBracket);

            position =
                closeBracket + 1;

            return new ExpressionNode(
                ExpressionKind.Set,
                "set",
                elements);
        }

        return null;
    }

    private static ExpressionNode?
        ParseIdentifierOrLiteral(
            IReadOnlyList<LexerToken> tokens,
            ref int position,
            int end)
    {
        if (position >= end)
        {
            return null;
        }

        LexerToken token =
            tokens[position];

        if (token.Kind == TokenKind.Identifier ||
            token.Kind == TokenKind.Keyword)
        {
            position++;

            return new ExpressionNode(
                ExpressionKind.Identifier,
                token.Text,
                Array.Empty<ExpressionNode>());
        }

        if (token.Kind == TokenKind.Number ||
            token.Kind == TokenKind.String ||
            token.Kind == TokenKind.Character)
        {
            position++;

            return new ExpressionNode(
                ExpressionKind.Literal,
                token.Text,
                Array.Empty<ExpressionNode>());
        }

        return null;
    }

    private static List<ExpressionNode> ParseArguments(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        var result =
            new List<ExpressionNode>();

        if (start >= end)
        {
            return result;
        }

        int currentStart =
            start;

        int roundDepth = 0;
        int squareDepth = 0;

        for (int i = start;
             i < end;
             i++)
        {
            string text =
                tokens[i].Text;

            if (text == "(")
            {
                roundDepth++;
            }
            else if (text == ")")
            {
                if (roundDepth > 0)
                {
                    roundDepth--;
                }
            }
            else if (text == "[")
            {
                squareDepth++;
            }
            else if (text == "]")
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
                }
            }

            if (text == "," &&
                roundDepth == 0 &&
                squareDepth == 0)
            {
                result.Add(
                    ParseRange(
                        tokens,
                        currentStart,
                        i));

                currentStart =
                    i + 1;
            }
        }

        // Вызов / set constructor без элементов
        // не должен создавать пустой элемент.
        if (currentStart < end)
        {
            result.Add(
                ParseRange(
                    tokens,
                    currentStart,
                    end));
        }

        return result;
    }

    private static int FindMatchingParenthesis(
        IReadOnlyList<LexerToken> tokens,
        int openIndex,
        int end)
    {
        int depth = 0;

        for (int i = openIndex;
             i < end;
             i++)
        {
            if (tokens[i].Text == "(")
            {
                depth++;
                continue;
            }

            if (tokens[i].Text == ")")
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static int FindMatchingBracket(
        IReadOnlyList<LexerToken> tokens,
        int openIndex,
        int end)
    {
        int depth = 0;

        for (int i = openIndex;
             i < end;
             i++)
        {
            if (tokens[i].Text == "[")
            {
                depth++;
                continue;
            }

            if (tokens[i].Text == "]")
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool IsMatchingOuterParentheses(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        int depth = 0;

        for (int i = start;
             i < end;
             i++)
        {
            if (tokens[i].Text == "(")
            {
                depth++;
            }
            else if (tokens[i].Text == ")")
            {
                depth--;

                if (depth == 0)
                {
                    return i == end - 1;
                }
            }
        }

        return false;
    }

    private static int BinaryPrecedence(string token) => token.ToLowerInvariant() switch
    {
        ".." => 0,
        "=" or "<>" or "<" or ">" or "<=" or ">=" or "in" or "is" => 1,
        "+" or "-" or "or" or "xor" => 2,
        "*" or "/" or "div" or "mod" or "and" or "shl" or "shr" or "as" => 3,
        _ => -1
    };

    private static int FindTopLevelBinaryOperator(
        IReadOnlyList<LexerToken> tokens, int start, int end)
    {
        int depth = 0;
        int weakest = int.MaxValue;
        int split = -1;
        for (int i = start; i < end; i++)
        {
            string token = tokens[i].Text;
            if (token is "(" or "[") { depth++; continue; }
            if (token is ")" or "]") { depth--; continue; }
            if (depth != 0 || i == start) continue;
            int precedence = BinaryPrecedence(token);
            if (precedence < 0 || precedence > weakest) continue;
            if (token is "+" or "-")
            {
                string previous = tokens[i - 1].Text;
                if (BinaryPrecedence(previous) >= 0 || IsUnaryOperator(previous) || previous == "@")
                    continue;
            }
            // Split at the rightmost operator in a group for left associativity.
            weakest = precedence;
            split = i;
        }
        return split;
    }

    private static int FindTopLevelOperator(
        IReadOnlyList<LexerToken> tokens,
        string text)
    {
        return FindTopLevelOperator(
            tokens,
            0,
            tokens.Count,
            text);
    }

    private static int FindTopLevelOperator(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end,
        string text)
    {
        int roundDepth = 0;
        int squareDepth = 0;

        for (int i = start;
             i < end;
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
                string.Equals(
                    current,
                    text,
                    StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsUnaryOperator(
        string text)
    {
        return string.Equals(
                   text,
                   "+",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   text,
                   "-",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   text,
                   "not",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static ExpressionNode EmptyNode()
    {
        return new ExpressionNode(
            ExpressionKind.Empty,
            string.Empty,
            Array.Empty<ExpressionNode>());
    }

    private static string JoinTokens(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        return string.Join(
            " ",
            tokens
                .Skip(start)
                .Take(end - start)
                .Select(x => x.Text));
    }
}
