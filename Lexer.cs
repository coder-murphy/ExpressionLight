using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 词法分析器：把字符流转换成 Token 列表。
/// 只做三件事：跳过空白、按最长匹配切词、记录位置。它不认识优先级，也不建树。
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    public Lexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public IReadOnlyList<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (true)
        {
            SkipWhiteSpace();

            if (IsAtEnd)
            {
                tokens.Add(new Token(TokenKind.End, "<eof>", _line, _column));
                return tokens;
            }

            var line = _line;
            var column = _column;
            var current = Current;

            if (IsDigit(current))
            {
                tokens.Add(ReadNumber(line, column));
            }
            else if (IsLetter(current) || current == '_')
            {
                tokens.Add(ReadIdentifier(line, column));
            }
            else if (current == '+' || current == '-' || current == '*' || current == '/')
            {
                Advance();
                tokens.Add(new Token(TokenKind.Operator, current.ToString(), line, column));
            }
            else
            {
                TokenKind kind;
                switch (current)
                {
                    case '(':
                        kind = TokenKind.LeftParen;
                        break;
                    case ')':
                        kind = TokenKind.RightParen;
                        break;
                    case '[':
                        kind = TokenKind.LeftBracket;
                        break;
                    case ']':
                        kind = TokenKind.RightBracket;
                        break;
                    case ',':
                        kind = TokenKind.Comma;
                        break;
                    default:
                        throw new ExpressionException(line, column, $"非法字符 '{current}'");
                }

                Advance();
                tokens.Add(new Token(kind, current.ToString(), line, column));
            }
        }
    }

    private Token ReadNumber(int line, int column)
    {
        var start = _index;

        while (IsDigit(Current))
        {
            Advance();
        }

        // 小数点只有在后面确实跟着数字时才属于这个数，避免把 "1." 切成合法数字。
        if (Current == '.')
        {
            Advance();
            if (!IsDigit(Current))
            {
                throw new ExpressionException(line, column, "小数点后缺少数字");
            }

            while (IsDigit(Current))
            {
                Advance();
            }
        }

        var text = _source.Substring(start, _index - start);
        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new ExpressionException(line, column, $"无法解析数字 '{text}'");
        }

        return new Token(TokenKind.Number, text, line, column, value);
    }

    private Token ReadIdentifier(int line, int column)
    {
        var start = _index;

        while (IsLetter(Current) || IsDigit(Current) || Current == '_')
        {
            Advance();
        }

        var text = _source.Substring(start, _index - start);
        return new Token(TokenKind.Identifier, text, line, column);
    }

    private void SkipWhiteSpace()
    {
        while (!IsAtEnd)
        {
            var current = Current;
            if (current != ' ' && current != '\t' && current != '\r' && current != '\n')
            {
                return;
            }

            Advance();
        }
    }

    private void Advance()
    {
        if (IsAtEnd)
        {
            return;
        }

        if (_source[_index] == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        _index++;
    }

    private bool IsAtEnd => _index >= _source.Length;

    private char Current => IsAtEnd ? '\0' : _source[_index];

    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }

    private static bool IsLetter(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }
}
