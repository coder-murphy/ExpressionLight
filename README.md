# ExpressionLight

一个分层实现的轻量表达式解析器：词法分析 → 语法分析 → AST → 求值。

## 支持的语法

- 十进制数：`1`、`1.5`
- 四则运算：`+ - * /`
- 一元正负：`-1`、`2*-3`
- 括号：`(1 + 2) * 3`
- 变量：`[price]`
- 函数：`abs(-2)`

```csharp
var expression = new SimpleExpression("([price] + 1) * 2");
Console.WriteLine(expression.Root);                  // (* (+ [price] 1) 2)

var variables = new Dictionary<string, decimal> { ["price"] = 12.5m };
Console.WriteLine(expression.Evaluate(variables));   // 27.0

if (!expression.TryEvaluate(variables, out var value, out var error))
{
    Console.WriteLine(error);                        // 带行号列号的错误信息
}
```

## 构建

```bash
dotnet build ExpressionLight.csproj
```

目标框架为 `netstandard2.1`，可直接被 .NET / Godot 4 / Unity 2021+ 引用。

## 项目结构

| 文件 | 职责 |
|---|---|
| `Token.cs` | 词法单元与类型 |
| `Lexer.cs` | 字符流 → Token 列表 |
| `Ast.cs` | 语法树节点 |
| `Parser.cs` | Token 列表 → AST（递归下降） |
| `Evaluator.cs` | AST → `decimal` |
| `SimpleExpression.cs` | 对外门面 |
| `ExpressionException.cs` | 带行列位置的统一异常 |

## 设计文档

分层原因、文法、递归下降、AST 与求值的完整说明见 [DESIGN.md](DESIGN.md)。

## License

见 [LICENSE](LICENSE)。
