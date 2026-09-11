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

            return new ExpressionNode(
                ExpressionKind.Binary,
                tokens[binaryIndex].Text,
                new[]
                {
                    left,
                    right
                });
        }

        // Unary operators.
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

        // Function / method call.
        //
        // Example:
        //   Trunc(Value)
        //   f_CurContext.DoAction(anActionData)
        int openParen =
            FindCallParenthesis(
                tokens,
                start,
                end);

        if (openParen > start)
        {
            int closeParen =
                FindMatchingParenthesis(
                    tokens,
                    openParen,
                    end);

            if (closeParen == end - 1)
            {
                var target =
                    ParseRange(
                        tokens,
                        start,
                        openParen);

                var arguments =
                    ParseArguments(
                        tokens,
                        openParen + 1,
                        closeParen);

                return new ExpressionNode(
                    ExpressionKind.Call,
                    "call",
                    new[]
                    {
                        target
                    }
                    .Concat(arguments)
                    .ToList());
            }
        }

        // Index access.
        //
        // Example:
        //   rData[1]
        //   c_DefaultFonts[I]
        //
        // Important: this is checked before member access so that
        // the indexed expression itself becomes a proper node.
        int bracketIndex =
            FindTopLevelOpenBracket(
                tokens,
                start,
                end);

        if (bracketIndex > start)
        {
            int closeBracket =
                FindMatchingBracket(
                    tokens,
                    bracketIndex,
                    end);

            if (closeBracket == end - 1)
            {
                var target =
                    ParseRange(
                        tokens,
                        start,
                        bracketIndex);

                var index =
                    ParseRange(
                        tokens,
                        bracketIndex + 1,
                        closeBracket);

                return new ExpressionNode(
                    ExpressionKind.IndexAccess,
                    "[]",
                    new[]
                    {
                        target,
                        index
                    });
            }
        }

        // Member access.
        //
        // Example:
        //   f_CurContext.DoAction
        //   c_DefaultFonts[I].rFontFileName
        int dotIndex =
            FindTopLevelMemberDot(
                tokens,
                start,
                end);

        if (dotIndex > start &&
            dotIndex < end - 1)
        {
            var left =
                ParseRange(
                    tokens,
                    start,
                    dotIndex);

            var right =
                ParseRange(
                    tokens,
                    dotIndex + 1,
                    end);

            return new ExpressionNode(
                ExpressionKind.MemberAccess,
                ".",
                new[]
                {
                    left,
                    right
                });
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

        int currentStart = start;
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

        // Вызов без аргументов:
        //
        // Foo()
        //
        // не должен создавать пустой аргумент.
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

    private static int FindCallParenthesis(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        int roundDepth = 0;
        int squareDepth = 0;

        for (int i = start;
             i < end;
             i++)
        {
            string text =
                tokens[i].Text;

            if (text == "[")
            {
                squareDepth++;
                continue;
            }

            if (text == "]")
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
                }

                continue;
            }

            if (text == "(")
            {
                if (roundDepth == 0 &&
                    squareDepth == 0)
                {
                    return i;
                }

                roundDepth++;
                continue;
            }

            if (text == ")")
            {
                if (roundDepth > 0)
                {
                    roundDepth--;
                }
            }
        }

        return -1;
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

    private static int FindTopLevelOpenBracket(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
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
                continue;
            }

            if (text == ")")
            {
                if (roundDepth > 0)
                {
                    roundDepth--;
                }

                continue;
            }

            if (text == "[")
            {
                if (roundDepth == 0 &&
                    squareDepth == 0)
                {
                    return i;
                }

                squareDepth++;
                continue;
            }

            if (text == "]")
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
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

    private static int FindTopLevelMemberDot(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
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
                continue;
            }

            if (text == ")")
            {
                if (roundDepth > 0)
                {
                    roundDepth--;
                }

                continue;
            }

            if (text == "[")
            {
                squareDepth++;
                continue;
            }

            if (text == "]")
            {
                if (squareDepth > 0)
                {
                    squareDepth--;
                }

                continue;
            }

            if (text == "." &&
                roundDepth == 0 &&
                squareDepth == 0)
            {
                return i;
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

    private static int FindTopLevelBinaryOperator(
        IReadOnlyList<LexerToken> tokens,
        int start,
        int end)
    {
        /*
         * Lowest precedence first.
         *
         * This is intentionally still a simplified Delphi
         * precedence table. It is enough for the current
         * structural expression layer.
         */
        string[] operators =
        {
            "or",
            "xor",
            "and",
            "=",
            "<>",
            "<",
            ">",
            "<=",
            ">=",
            "+",
            "-",
            "*",
            "/",
            "div",
            "mod",
            "shl",
            "shr"
        };

        foreach (string op in operators)
        {
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
                    continue;
                }

                if (text == ")")
                {
                    if (roundDepth > 0)
                    {
                        roundDepth--;
                    }

                    continue;
                }

                if (text == "[")
                {
                    squareDepth++;
                    continue;
                }

                if (text == "]")
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
                        text,
                        op,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
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
