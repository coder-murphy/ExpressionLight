using System;
using System.Collections.Generic;

/// <summary>
/// 递归下降解析器。每个产生式对应一个方法，调用层级直接表达优先级：
/// expression -> term -> unary -> primary。
/// </summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public ExpressionNode Parse()
    {
        var expression = ParseExpression();
        Expect(TokenKind.End, "表达式结束");
        return expression;
    }

    private Token Current => _tokens[_position];

    private Token Advance()
    {
        var token = Current;
        if (token.Kind != TokenKind.End)
        {
            _position++;
        }

        return token;
    }

    private bool IsOperator(string text)
    {
        return Current.Kind == TokenKind.Operator && Current.Text == text;
    }

    /// <summary>expr := term (('+' | '-') term)*</summary>
    private ExpressionNode ParseExpression()
    {
        var left = ParseTerm();

        while (IsOperator("+") || IsOperator("-"))
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseTerm(), op.Line, op.Column);
        }

        return left;
    }

    /// <summary>term := unary (('*' | '/') unary)*</summary>
    private ExpressionNode ParseTerm()
    {
        var left = ParseUnary();

        while (IsOperator("*") || IsOperator("/"))
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseUnary(), op.Line, op.Column);
        }

        return left;
    }

    /// <summary>unary := ('+' | '-') unary | primary</summary>
    private ExpressionNode ParseUnary()
    {
        if (IsOperator("+") || IsOperator("-"))
        {
            var op = Advance();
            return new UnaryNode(op.Text, ParseUnary(), op.Line, op.Column);
        }

        return ParsePrimary();
    }

    /// <summary>
    /// primary := NUMBER
    ///          | '[' IDENT ']'
    ///          | IDENT '(' [expr (',' expr)*] ')'
    ///          | '(' expr ')'
    /// </summary>
    private ExpressionNode ParsePrimary()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new NumberNode(token.Value, token.Line, token.Column);

            case TokenKind.LeftBracket:
            {
                Advance();
                var name = Expect(TokenKind.Identifier, "变量名");
                Expect(TokenKind.RightBracket, "']'");
                return new VariableNode(name.Text, name.Line, name.Column);
            }

            case TokenKind.Identifier:
            {
                Advance();
                if (Current.Kind != TokenKind.LeftParen)
                {
                    throw Error(token, $"标识符 '{token.Text}' 后缺少 '('");
                }

                Advance();
                var arguments = new List<ExpressionNode>();
                if (Current.Kind != TokenKind.RightParen)
                {
                    arguments.Add(ParseExpression());
                    while (Current.Kind == TokenKind.Comma)
                    {
                        Advance();
                        arguments.Add(ParseExpression());
                    }
                }

                Expect(TokenKind.RightParen, "')'");
                return new CallNode(token.Text, arguments, token.Line, token.Column);
            }

            case TokenKind.LeftParen:
            {
                Advance();
                var inner = ParseExpression();
                Expect(TokenKind.RightParen, "')'");
                return inner;
            }

            default:
                throw Error(token, $"期望表达式，实际得到 {Describe(token)}");
        }
    }

    private Token Expect(TokenKind kind, string expected)
    {
        if (Current.Kind != kind)
        {
            throw Error(Current, $"期望{expected}，实际得到 {Describe(Current)}");
        }

        return Advance();
    }

    private static ExpressionException Error(Token token, string message)
    {
        return new ExpressionException(token.Line, token.Column, message);
    }

    private static string Describe(Token token)
    {
        return token.Kind == TokenKind.End ? "输入结束" : $"'{token.Text}'";
    }
}
