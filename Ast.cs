using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 语法树节点。与 Token 的区别：Token 是平的词法单元，节点是嵌套的语法结构。
/// 每个节点都保留源码位置，运行时错误也能指回具体位置。
/// </summary>
public abstract class ExpressionNode
{
    protected ExpressionNode(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }

    /// <summary>输出 S 表达式形式，便于测试和观察树形。</summary>
    public abstract override string ToString();
}

public sealed class NumberNode : ExpressionNode
{
    public NumberNode(decimal value, int line, int column)
        : base(line, column)
    {
        Value = value;
    }

    public decimal Value { get; }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class VariableNode : ExpressionNode
{
    public VariableNode(string name, int line, int column)
        : base(line, column)
    {
        Name = name;
    }

    public string Name { get; }

    public override string ToString()
    {
        return $"[{Name}]";
    }
}

public sealed class UnaryNode : ExpressionNode
{
    public UnaryNode(string op, ExpressionNode operand, int line, int column)
        : base(line, column)
    {
        Operator = op;
        Operand = operand;
    }

    public string Operator { get; }

    public ExpressionNode Operand { get; }

    public override string ToString()
    {
        return $"({Operator} {Operand})";
    }
}

public sealed class BinaryNode : ExpressionNode
{
    public BinaryNode(string op, ExpressionNode left, ExpressionNode right, int line, int column)
        : base(line, column)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public string Operator { get; }

    public ExpressionNode Left { get; }

    public ExpressionNode Right { get; }

    public override string ToString()
    {
        return $"({Operator} {Left} {Right})";
    }
}

public sealed class CallNode : ExpressionNode
{
    public CallNode(string name, IReadOnlyList<ExpressionNode> arguments, int line, int column)
        : base(line, column)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; }

    public IReadOnlyList<ExpressionNode> Arguments { get; }

    public override string ToString()
    {
        return Arguments.Count == 0
            ? $"({Name})"
            : $"({Name} {string.Join(" ", Arguments)})";
    }
}
