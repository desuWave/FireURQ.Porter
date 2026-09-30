namespace FireURQ.Porter.Lexer;

public enum TokenKind
{
    Identifier,
    Keyword,
    String,
    Number,
    Character,
    Symbol,
    Operator,
    Comment,
    Directive,
    EndOfFile
}

public sealed record LexerToken(
    TokenKind Kind,
    string Text,
    int Line,
    int Column);