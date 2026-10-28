using System;
using System.Collections.Generic;

/// <summary>
/// 求值器：只负责遍历语法树并计算，不再关心字符、括号或优先级。
/// 变量缺失、除零、未知函数等错误都在这里带位置抛出。
/// </summary>
public sealed class Evaluator
{
    private static readonly IReadOnlyDictionary<string, decimal> EmptyVariables =
        new Dictionary<string, decimal>();

    private static readonly Dictionary<string, FunctionDefinition> Functions =
        new Dictionary<string, FunctionDefinition>(StringComparer.Ordinal)
        {
            ["abs"] = new FunctionDefinition(1, values => Math.Abs(values[0])),
        };

    private readonly IReadOnlyDictionary<string, decimal> _variables;

    public Evaluator(IReadOnlyDictionary<string, decimal>? variables = null)
    {
        _variables = variables ?? EmptyVariables;
    }

    public decimal Evaluate(ExpressionNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        switch (node)
        {
            case NumberNode number:
                return number.Value;
            case VariableNode variable:
                return EvaluateVariable(variable);
            case UnaryNode unary:
                return EvaluateUnary(unary);
            case BinaryNode binary:
                return EvaluateBinary(binary);
            case CallNode call:
                return EvaluateCall(call);
            default:
                throw new InvalidOperationException($"未知的语法树节点：{node.GetType().Name}");
        }
    }

    private decimal EvaluateVariable(VariableNode variable)
    {
        if (_variables.TryGetValue(variable.Name, out var value))
        {
            return value;
        }

        throw new ExpressionException(variable.Line, variable.Column, $"未定义的变量 '{variable.Name}'");
    }

    private decimal EvaluateUnary(UnaryNode unary)
    {
        var operand = Evaluate(unary.Operand);
        return unary.Operator == "-" ? -operand : operand;
    }

    private decimal EvaluateBinary(BinaryNode binary)
    {
        var left = Evaluate(binary.Left);
        var right = Evaluate(binary.Right);

        switch (binary.Operator)
        {
            case "+":
                return left + right;
            case "-":
                return left - right;
            case "*":
                return left * right;
            case "/":
                if (right == 0m)
                {
                    throw new ExpressionException(binary.Line, binary.Column, "除数不能为零");
                }

                return left / right;
            default:
                throw new ExpressionException(binary.Line, binary.Column, $"未知运算符 '{binary.Operator}'");
        }
    }

    private decimal EvaluateCall(CallNode call)
    {
        if (!Functions.TryGetValue(call.Name, out var function))
        {
            throw new ExpressionException(call.Line, call.Column, $"未定义的函数 '{call.Name}'");
        }

        if (call.Arguments.Count != function.ArgumentCount)
        {
            throw new ExpressionException(
                call.Line,
                call.Column,
                $"函数 '{call.Name}' 需要 {function.ArgumentCount} 个参数，实际传入 {call.Arguments.Count} 个");
        }

        var arguments = new decimal[call.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Evaluate(call.Arguments[i]);
        }

        return function.Invoke(arguments);
    }

    private sealed class FunctionDefinition
    {
        public FunctionDefinition(int argumentCount, Func<decimal[], decimal> invoke)
        {
            ArgumentCount = argumentCount;
            Invoke = invoke;
        }

        public int ArgumentCount { get; }

        public Func<decimal[], decimal> Invoke { get; }
    }
}
