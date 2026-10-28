using System;

/// <summary>
/// 词法单元的种类。词法层只负责把字符流切成这些类别，不关心它们如何组合。
/// </summary>
public enum TokenKind
{
    Number,
    Identifier,
    Operator,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    End,
}

/// <summary>
/// 一个词法单元。Text 是原始词素，Line/Column 用于错误定位。
/// 词法单元是“平”的数据，不持有父子关系。
/// </summary>
public sealed class Token
{
    public Token(TokenKind kind, string text, int line, int column, decimal value = 0m)
    {
        Kind = kind;
        Text = text;
        Line = line;
        Column = column;
        Value = value;
    }

    public TokenKind Kind { get; }

    public string Text { get; }

    public int Line { get; }

    public int Column { get; }

    /// <summary>仅当 Kind 为 Number 时有效。</summary>
    public decimal Value { get; }

    public override string ToString()
    {
        return $"{Kind}('{Text}')@{Line}:{Column}";
    }
}
