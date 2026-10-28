using System;
using System.Collections.Generic;

/// <summary>
/// 门面：把词法、语法、求值三层串起来，对外保持一个简单入口。
/// 构造时完成词法和语法校验；求值阶段可以传入变量表。
/// </summary>
public sealed class SimpleExpression
{
    public SimpleExpression(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Source = source;
        Tokens = Tokenize(source);
        Root = new Parser(Tokens).Parse();
    }

    public string Source { get; }

    public IReadOnlyList<Token> Tokens { get; }

    public ExpressionNode Root { get; }

    public static IReadOnlyList<Token> Tokenize(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Lexer(source).Tokenize();
    }

    public static ExpressionNode Parse(string source)
    {
        return new Parser(Tokenize(source)).Parse();
    }

    public decimal Evaluate(IReadOnlyDictionary<string, decimal>? variables = null)
    {
        return new Evaluator(variables).Evaluate(Root);
    }

    public bool TryEvaluate(out decimal result, out string error)
    {
        return TryEvaluate(null, out result, out error);
    }

    public bool TryEvaluate(IReadOnlyDictionary<string, decimal>? variables, out decimal result, out string error)
    {
        try
        {
            result = Evaluate(variables);
            error = string.Empty;
            return true;
        }
        catch (ExpressionException exception)
        {
            result = 0m;
            error = exception.Message;
            return false;
        }
    }
}
