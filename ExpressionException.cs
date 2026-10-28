using System;

/// <summary>
/// 词法、语法、求值三个阶段共用的异常。位置信息让调用方可以直接定位到出错字符。
/// </summary>
public sealed class ExpressionException : Exception
{
    public ExpressionException(int line, int column, string message)
        : base($"第 {line} 行第 {column} 列：{message}")
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}
