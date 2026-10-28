# ExpressionLight 设计文档

这份文档解释重构后的实现为什么这样分层，以及每一层分别解决什么问题。阅读顺序建议：先看第 2 节的流水线，再按词法、文法、语法、AST、求值的顺序看下去。

代码规模很小，但每一层都只做一件事。旧版的核心问题不是某个具体 bug，而是把三层职责压进了一个 `for` 循环，导致"字符校验规则"被迫承担了文法的职责。

---

## 1. 目标与范围

支持的语言：

- 十进制数：`1`、`1.5`
- 四则运算：`+ - * /`
- 一元正负号：`-1`、`+-2`
- 括号分组：`(1 + 2) * 3`
- 变量：`[price]`
- 函数调用：`abs(-2)`、`max(1, 2)`（函数由求值层注册，目前内置 `abs`）
- 空白（空格、Tab、CR、LF）只起分隔作用

有意不支持：

- 多表达式、赋值、语句块
- 字符串、布尔、比较、位运算
- 自定义函数注册（目前是求值器内部的私有表）
- 错误恢复（当前是 fail-fast）

边界划小是刻意的：先把"一个表达式"这条链路做正确，扩展才有意义。

---

## 2. 总体架构

```
字符流 "1 + 2 * 3"
   │
   │  Lexer        规则：最长匹配、跳过空白、记录位置
   ▼
Token 流          [Number(1)] [Operator(+)] [Number(2)] [Operator(*)] [Number(3)] [End]
   │
   │  Parser       规则：上下文无关文法、递归、左结合循环
   ▼
AST               Binary(+)
                  ├── Number(1)
                  └── Binary(*)
                      ├── Number(2)
                      └── Number(3)
   │
   │  Evaluator    规则：按节点类型递归求值
   ▼
decimal 7
```

四层的契约：

| 层 | 输入 | 输出 | 它不做什么 |
|---|---|---|---|
| Lexer | `string` | 平的 `Token` 列表 | 不判断 token 顺序、不建树、不认识优先级 |
| Parser | `Token` 列表 | 嵌套的 `ExpressionNode` | 不关心字符、不认识数字字面量、不做计算 |
| Evaluator | AST + 变量表 | `decimal` | 不关心源码文本、不做语法校验 |
| 门面 | `string` | 解析结果 / 数值 | 只负责把三层串起来 |

一个简单的判断标准：

- 如果一段逻辑只需要看"当前字符/下一个字符"，它属于词法。
- 如果一段逻辑需要看"嵌套关系、先后顺序、优先级"，它属于语法。
- 如果一段逻辑需要看"值"，它属于求值。

旧版的 `CheckSyntaxBetweenTwoChars(prev, current)` 本质是一个有限状态机，属于词法层，却被用来判断括号是否配对、表达式是否完整——这些是上下文无关文法才能表达的东西，所以它必然不断打补丁。

---

## 3. 词法分析（Lexer）

实现见 [`Lexer.cs`](Lexer.cs) 与 [`Token.cs`](Token.cs)。

### 3.1 Token 模型

```csharp
public enum TokenKind
{
    Number, Identifier, Operator,
    LeftParen, RightParen, LeftBracket, RightBracket, Comma,
    End,
}
```

`Token` 只保存四类信息：

| 字段 | 作用 |
|---|---|
| `Kind` | 词法类别，Parser 只认这个 |
| `Text` | 原始词素，用于报错和调试 |
| `Line` / `Column` | 1 基位置，贯穿到 AST 和运行时错误 |
| `Value` | 仅 `Number` 使用，已解析成 `decimal` |

关键点：**Token 是平的数据，没有 `Parent` / `Children`**。旧版让同一个 `Token` 类既当词法单元又当树节点，还在构造函数里自动挂到父节点，这是重复子节点和错误树形的根源。

`End` 是哨兵 token，让 Parser 不必到处判断数组越界，也让"输入结束"成为一条正常的语法错误信息。

### 3.2 扫描算法

主循环非常短：

1. 跳过空白。
2. 到达末尾：产出一个 `End`，结束。
3. 当前字符是数字：`ReadNumber`。
4. 当前字符是字母或下划线：`ReadIdentifier`。
5. 当前字符是运算符/括号/逗号：产出单字符 token。
6. 其他：抛 `ExpressionException`。

三条规则值得单独说明：

**最长匹配（maximal munch）**：读数字时一直读到不再是数字为止，而不是"先读一个字符，再看下一个是不是运算符"。这样 `12` 是一个 token，`123` 不会在行尾丢失。旧版正是在行尾忘了 flush，导致 `1+2` 的 `2` 消失。

**空白是分隔符，不只是可忽略字符**：`1 2` 必须切成两个 `Number`，让 Parser 报"意外的数字"，而不是悄悄拼成 `12`。旧版 `continue` 跳过空格却不更新 `lastChar`，把两个词粘在一起。

**小数点需要后顾**：只有 `.` 后面确实跟着数字，小数点才属于当前数字，所以 `1.` 会在"小数点后缺少数字"处失败，`1..2` 也不会被切成合法数字。这类"看一眼下一个字符"的判断合法地留在词法层，因为它是正则规则。

### 3.3 位置跟踪

`Advance()` 统一维护 `_index / _line / _column`：遇到 `\n` 换行并重置列号，其他字符列号加一。所有错误都通过 `ExpressionException(line, column, message)` 抛出，最终消息形如：

```
第 1 行第 4 列：期望表达式结束，实际得到 ']'
```

旧版的 `Split('\n')` 会把 Windows 的 `\r` 留在行尾，变成一个非法字符；现在 `\r` 和 `\n` 都只是空白，CRLF 输入可以直接工作。

词法层完全不判断 token 的先后关系，所以 `+1`、`1+2]`、`2(3)` 都能正常产出 token 流，错误留给语法层用统一的方式报告。

---

## 4. 文法

语言用 EBNF 描述如下，这就是 Parser 与语言之间的契约：

```ebnf
program   := expr EOF

expr      := term (('+' | '-') term)*
term      := unary (('*' | '/') unary)*
unary     := ('+' | '-') unary | primary
primary   := NUMBER
           | '[' IDENT ']'
           | IDENT '(' [ expr (',' expr)* ] ')'
           | '(' expr ')'

NUMBER    := DIGIT+ ('.' DIGIT+)?
IDENT     := (LETTER | '_') (LETTER | DIGIT | '_')*
```

### 4.1 优先级写在文法里

文法层级从外到内是 `expr → term → unary → primary`。越靠近 `primary` 的运算结合得越紧：

- `primary` 是原子（数字、变量、函数、括号）
- `unary` 比 `term` 紧，所以 `-2*3` 是 `(-2)*3`
- `term` 比 `expr` 紧，所以 `1+2*3` 是 `1+(2*3)`

`1+2*3` 的推导过程：

```
expr
├── term → 1
├── '+'
└── term
    ├── unary → 2
    ├── '*'
    └── unary → 3
```

对应 AST：

```
(+ 1 (* 2 3))
```

旧版把所有 token 挂在同一层，`+` 和 `*` 是兄弟节点，因此无论求值怎么写都不可能得到正确优先级。

### 4.2 左结合与右结合

`expr := term (('+'|'-') term)*` 里的 `(...)*` 表示"可以重复零次或多次"，在代码里就是一个 `while` 循环，每轮把"左值 + 运算符 + 右值"包成一个新节点，于是 `1-2-3` 是 `(1-2)-3`（左结合）。

`unary := ('+'|'-') unary | primary` 是直接递归，表示前缀运算符右结合，于是 `--1` 是 `-(-1)`。

在递归下降里不能直接照着 `expr := expr '+' term` 写，那会无限递归；改写为循环是标准做法。

---

## 5. 语法分析（Parser）

实现见 [`Parser.cs`](Parser.cs)。

### 5.1 产生式与方法一一对应

| 产生式 | 方法 | 说明 |
|---|---|---|
| `program` | `Parse` | 解析表达式后必须遇到 `End` |
| `expr` | `ParseExpression` | `+ -` 循环，最低优先级 |
| `term` | `ParseTerm` | `* /` 循环 |
| `unary` | `ParseUnary` | 前缀 `+ -` 递归 |
| `primary` | `ParsePrimary` | 数字、变量、函数、括号 |

这种写法叫递归下降（recursive descent）。它的可读性来自"文法怎么写，代码就怎么写"，不需要额外的规则表。

### 5.2 游标与期望

Parser 只维护一个游标 `_position`，三个小工具：

- `Current`：当前 token
- `Advance()`：消费当前 token（`End` 不前进）
- `Expect(kind, expected)`：断言并消费，否则抛错

`Expect` 是所有错误信息的统一出口。例如：

| 输入 | 错误 |
|---|---|
| `1+` | `第 1 行第 3 列：期望表达式，实际得到 输入结束` |
| `1+2]` | `第 1 行第 4 列：期望表达式结束，实际得到 ']'` |
| `[]` | `第 1 行第 2 列：期望变量名，实际得到 ']'` |
| `abs` | `第 1 行第 1 列：标识符 'abs' 后缺少 '('` |
| `abs(1 2)` | `第 1 行第 7 列：期望')'，实际得到 '2'` |
| `2(3)` | `第 1 行第 2 列：期望表达式结束，实际得到 '('` |

对比旧版：`1+` 和 `1+ `（多一个尾随空格）一个报错一个通过，`abs` 在 `'s'` 位置报错，未匹配的 `]` 直接 `NullReferenceException`。差别不在"错误处理写得好不好"，而在**错误来自文法还是来自邻接字符猜测**。

### 5.3 四类原子的解析细节

**括号** `'(' expr ')'`：解析内部表达式后强制消费 `)`。分组在 AST 里是透明的——`(1+2)` 和 `1+2` 产生同一个节点，括号只影响解析顺序。

**变量** `'[' IDENT ']'`：`[` 之后必须是标识符，之后必须是 `]`。变量名不能为空，也不能是数字。

**函数** `IDENT '(' args ')'`：标识符之后**必须**紧跟 `(`，否则报错（不支持"裸标识符"当变量用）。参数允许为空列表，参数个数在求值层检查，因为语法层不知道函数签名。

**数字**：直接使用词法阶段已解析好的 `Value`，Parser 不再碰字符串和 `decimal.Parse`。

### 5.4 为什么语法层不校验"相邻字符"

有限状态机只能记住"前一个字符是什么"，无法知道"当前在第几层括号里"。所以：

- `((((1))))` 这种嵌套，邻接规则数不出来
- `1+2*3` 的优先级，邻接规则表达不了
- `1+2]` 的配对，邻接规则只能靠"最后一位必须是数字/括号"这种位置规则兜底，于是必然出现自相矛盾的接受/拒绝

把这些交给递归下降 + 文法，上述问题全部变成统一的 `Expect` 错误。

---

## 6. AST 设计

实现见 [`Ast.cs`](Ast.cs)。

### 6.1 Token 与 AST 是两种东西

| | Token | AST 节点 |
|---|---|---|
| 形状 | 平的列表 | 嵌套的树 |
| 内容 | 词素文本 + 位置 | 结构关系 + 位置 |
| 谁生产 | Lexer | Parser |
| 谁消费 | Parser | Evaluator |

旧版把两者合成一个 `Token` 类，于是"挂父子节点"这种语法动作混进了词法对象的构造函数里。分开之后，Lexer 永远不会修改树，Parser 也不再需要字符信息。

节点类型：

| 节点 | 字段 | `ToString()` 示例 |
|---|---|---|
| `NumberNode` | `Value` | `1.5` |
| `VariableNode` | `Name` | `[price]` |
| `UnaryNode` | `Operator`、`Operand` | `(- 2)` |
| `BinaryNode` | `Operator`、`Left`、`Right` | `(+ 1 (* 2 3))` |
| `CallNode` | `Name`、`Arguments` | `(abs (- 2))` |

`ToString()` 输出 S 表达式，只用于测试和调试，求值不依赖它。

### 6.2 位置信息贯穿到运行时

每个 AST 节点都保留 `Line` / `Column`：

- `BinaryNode` 记录运算符位置，所以 `1/0` 的错误指向 `/`
- `VariableNode` 记录变量名位置，所以未定义变量指向名字本身
- `CallNode` 记录函数名位置，所以未知函数、参数个数错误都指向函数名

这就是"解析阶段产生的信息，应该在运行阶段还能用"的具体体现。

---

## 7. 求值（Evaluator）

实现见 [`Evaluator.cs`](Evaluator.cs)。

求值器只做一件事：按节点类型递归。

```csharp
switch (node)
{
    case NumberNode number:  return number.Value;
    case VariableNode v:     return EvaluateVariable(v);
    case UnaryNode u:        return EvaluateUnary(u);
    case BinaryNode b:       return EvaluateBinary(b);
    case CallNode c:         return EvaluateCall(c);
}
```

几个设计选择：

**用 `decimal` 而不是 `double`**：表达式引擎常用于金额和数量，`0.1 + 0.2` 应该等于 `0.3`。代价是运算速度略慢、范围较小，这个权衡在业务公式场景是合理的。

**除零显式报错**：`decimal` 除以零会抛 `DivideByZeroException`，这里提前检查并转成带位置的 `ExpressionException`。

**变量表是只读字典**：`IReadOnlyDictionary<string, decimal>`，缺失变量报"未定义的变量 'x'"，位置指向 `[x]`。

**函数表是私有注册表**：键为函数名，值为"参数个数 + 实现"，目前只有 `abs`。参数个数不匹配会在调用前报错，而不是让函数内部 `values[0]` 越界。函数参数从左到右求值。

**求值是纯的**：没有副作用、没有全局状态（注册表是只读使用的静态字段），同一个 AST 可以反复求值，也可以并行求值不同实例。

---

## 8. 门面与公开 API

实现见 [`SimpleExpression.cs`](SimpleExpression.cs)。

| 成员 | 作用 |
|---|---|
| `SimpleExpression(string source)` | 完成词法 + 语法校验，失败抛 `ExpressionException` |
| `Source` | 原始文本 |
| `Tokens` | 完整 Token 流（含 `End`），便于教学和调试 |
| `Root` | AST 根节点 |
| `static Tokenize(source)` | 只做词法 |
| `static Parse(source)` | 词法 + 语法，返回 AST |
| `Evaluate(variables = null)` | 求值，失败抛 `ExpressionException` |
| `TryEvaluate(out result, out error)` | 求值的非异常版本 |
| `TryEvaluate(variables, out result, out error)` | 带变量表的非异常版本 |

用法：

```csharp
var expression = new SimpleExpression("([price] + 1) * 2");
Console.WriteLine(expression.Root);      // (* (+ [price] 1) 2)

var variables = new Dictionary<string, decimal> { ["price"] = 12.5m };
Console.WriteLine(expression.Evaluate(variables));   // 27.0

if (!expression.TryEvaluate(variables, out var result, out var error))
{
    Console.WriteLine(error);
}
```

### 8.1 相对旧版的 API 变化

| 旧版 | 新版 | 原因 |
|---|---|---|
| `new SimpleExpression()` 空构造 | 移除 | 空表达式没有语义；传空字符串会在解析时报"期望表达式" |
| `Buffer` 可写、`Init` 手动解析 | `Source` 只读，构造即解析 | 避免改完 Buffer 忘记重新解析、旧树残留 |
| `Lines`、多行 | 换行视作空白，单表达式 | 旧版多行实际只保留最后一行，没有可用语义 |
| `Tokens` 返回 Token 树 | `Tokens` 返回平的 Token 列表，`Root` 返回 AST | 词法与语法分离 |
| `Compute(IDictionary<string, object>, ref object, ref string)` | `Evaluate` / `TryEvaluate`，值为 `decimal` | `ref` 参数和 `object` 装箱不适合公式引擎 |
| 解析失败可能抛 NRE / 其他异常 | 统一 `ExpressionException`，带行号列号 | 错误类型可预期 |

---

## 9. 旧问题与新行为对照

| 旧版输入 | 旧版行为 | 新版行为 | 解决它的设计 |
|---|---|---|---|
| `123` | 空树 | `123` | 词法最长匹配，行尾无需特殊 flush |
| `1+2` | 常量 1 + 运算符，2 丢失 | 3 | 同上 |
| `1+2*3` | 四个兄弟节点，`+2` 常量 | AST `(+ 1 (* 2 3))` = 7 | 文法层级 + AST |
| `(1+2)*3` | 常量文本 `"(1"`，`)` 被丢弃 | 9 | 括号是独立 token，`)` 被 `Expect` 消费 |
| `[a]+1` | 变量名 `"[a"` | 6 | 词法边界正确 |
| `abs(1)` | 函数下两个空子节点 | 1 | Token 不再自动挂父节点 |
| `1 2` | 空树，静默成功 | 报错：`期望表达式结束，实际得到 '2'` | 空白是分隔符 |
| `1a` | 行尾报非法字符 | 报错：`期望表达式结束，实际得到 'a'` | 词法与语法各司其职 |
| `2(3)` | 静默接受，空节点 | 报错：`期望表达式结束，实际得到 '('` | 不支持隐式乘法，文法没有这条产生式 |
| `1+2]` | `NullReferenceException` | 报错：`期望表达式结束，实际得到 ']'` | 栈/游标配对，不再手动 `Parent` |
| `1+2\r\n` | 非法字符 `\r` | 3 | 空白集合包含 `\r\n` |
| `1+` / `1+ ` | 前者报错、后者通过 | 都报错 | 错误由文法决定，与尾随空白无关 |
| `1+2\n3*4` | 只剩 `3*4` | 明确报错：第 2 行第 1 列 | 不再静默丢数据 |
| `new SimpleExpression()` | `NullReferenceException` | 构造要求非 null；空串解析报错 | 显式参数校验 |
| `Compute(...)` | 永远返回 true、result 不变 | 正确计算 | 真正的 AST 求值 |
| 整个文件 | 缺 using，编译失败 | `netstandard2.1` 工程 0 警告 0 错误 | 显式 using + csproj |

---

## 10. 复杂度与限制

- 词法：时间 `O(n)`，空间 `O(n)`（Token 列表）。
- 语法：时间 `O(n)`，递归深度与括号嵌套深度成正比，空间 `O(n)`。
- 求值：时间 `O(n)`，递归深度与 AST 高度成正比。
- 没有正则回溯，没有重复扫描，也没有"每读一个字符就回看整行"的 `Substring`。

已知限制：

- 超深嵌套（例如数万层括号）会触发栈溢出。递归下降的固有代价；如果确实需要，可以改成显式栈或设置嵌套上限。
- 函数注册表是私有的，扩展需要修改源码。
- 不支持多表达式，因此也没有语句分隔符和注释语法。
- 错误是 fail-fast，一次只报一个。

这些限制都是当前范围内的有意取舍，不是遗漏。

---

## 11. 可以怎样继续扩展

按"先改文法，再改词法，最后改求值"的顺序扩展，能让每一层的变化都可测试：

| 想加的功能 | 需要改的地方 |
|---|---|
| 新二元运算符（如 `%`） | 词法加 token；文法加一层或扩展现有层；求值加 case |
| 新函数 | 只在求值器的函数表加一项（若要开放给使用者，再把表改成可注入） |
| 自定义变量 | 已经在门面层支持，通过 `IReadOnlyDictionary<string, decimal>` 传入 |
| 字符串/布尔 | 词法加字符串 token；文法加 primary 分支；值类型从 `decimal` 升级为联合类型 |
| 多表达式/赋值 | 文法新增 `program := statement+`；需要语句分隔符和符号表 |
| 更好的错误 | 在 `Expect` 里附加"最近的合法 token 集合"，或做错误恢复 |

建议每次扩展先写文法，再写失败用例，最后写实现。先写测试的好处是：错误信息是否指向正确位置，只有用例能告诉你。

---

## 12. 已验证的用例

重构完成后用独立测试工程验证了 28 个用例，全部通过。核心覆盖：

词法：

- `1+2*3` → `Number:1 Operator:+ Number:2 Operator:* Number:3 End`
- `1 2` → 两个独立 `Number`
- `1+2\r\n` → 3

语法与求值：

- `123` → 123
- `1+2*3` → `(+ 1 (* 2 3))` = 7
- `(1+2)*3` → `(* (+ 1 2) 3)` = 9
- `1.5+2` → 3.5
- `-1+2` → 1
- `--1` → 1
- `1++2` → 3
- `abs(-2)` → 2
- `[a]+1`（a=5）→ 6
- `([price]+1)*2`（price=12.5）→ 27.0

错误路径：

- `1+` → 期望表达式，实际得到输入结束
- `1+2]` → 期望表达式结束，实际得到 `']'`
- `abs(1 2)` → 期望 `')'`
- `abs` → 标识符后缺少 `'('`
- `[]` → 期望变量名
- `2(3)`、`1a` → 期望表达式结束
- `1+2\n3*4` → 第 2 行第 1 列
- `1.` → 小数点后缺少数字
- `1/0` → 除数不能为零
- `[x]+1` → 未定义的变量
- `foo(1)` → 未定义的函数
- `abs()` → 参数个数不匹配
- `TryEvaluate` 的成功与失败路径

---

## 13. 一句话总结

词法回答"有哪些词"，语法回答"这些词能怎样组合"，求值回答"组合出来是什么值"。把这三个问题分别放进三个类，每一层都可以单独测试，错误也自然带着正确的位置。旧版的困难不在于 C# 写不出来，而在于用一个 `for` 循环同时回答了三个问题。
