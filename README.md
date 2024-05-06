# ExpressionLight
轻量级自定义表达式解析器

public sealed class SimpleExpression
{
    /// <summary>
    /// 词条类型
    /// </summary>
    public enum TokenTypes
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown,

        /// <summary>
        /// 常量
        /// </summary>
        Const,

        /// <summary>
        /// 函数
        /// </summary>
        Function,

        /// <summary>
        /// 变量
        /// </summary>
        Variable,

        /// <summary>
        /// 运算符
        /// </summary>
        Operator,

        /// <summary>
        /// 括号
        /// </summary>
        Brace,
    }

    public string Buffer
    {
        get => _buffer;
        set
        {
            _buffer = value;
            Lines = _buffer.Split('\n');
        }
    }

    public Token Tokens
    {
        get => _tokens;
    }

    public string[] Lines { get; private set; }

    public SimpleExpression(string buffer)
    {
        Buffer = buffer;
        if (!string.IsNullOrWhiteSpace(buffer) && !Init(out var errMsg))
        {
            throw new ArgumentException(errMsg);
        }
    }

    public SimpleExpression() : this(null) { }

    public bool Init(out string msg)
    {
        msg = "";
        for (var lineIndex = 0; lineIndex < Lines.Length; lineIndex++)
        {
            if (!ParseNodes(Lines[lineIndex], lineIndex, out msg))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// compute the result according to the parameter table.
    /// </summary>
    /// <param name="paramsTable"></param>
    /// <param name="result"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    public bool Compute(IDictionary<string, object> paramsTable, ref object result, ref string msg)
    {
        var temp = Tokens;
        if(temp.Tokens.Count > 0)
        {
            switch (temp.Type)
            {
                case TokenTypes.Function:
                    if(!functions.TryGetValue(temp.Content,out var func))
                    {
                        msg = $"function:'{temp.Content}' is not supported.";
                        return false;
                    }
                    var input = GetTokenValue(temp, paramsTable);
                    result = func(new object[] { input });
                    break;
            }
        }
        else
        {
            if(temp.Parent != null)
            {

            }
        }
        return true;
    }

    #region static methods
    private object GetTokenValue(Token token, IDictionary<string, object> paramsTable)
    {
        Token last = null;
        Token next = null;
        var lastVal = decimal.Zero;
        var resVal = decimal.Zero;
        if(token.Count() == 0)
        {
            switch(token.Type)
            {
                case TokenTypes.Const:
                    return Convert.ToDecimal(token.Content);
                case TokenTypes.Variable:
                    _ = paramsTable.TryGetValue(token.Content, out var val);
                    return val;
            }
            return resVal;
        }
        foreach(var t in token)
        {
            next = token;
            if(last == null)
            {
                continue;
            }
            switch(next.Type)
            {
                case TokenTypes.Operator:
                    lastVal = Convert.ToDecimal(GetTokenValue(last, paramsTable));
                    break;
            }
            last = token;
        }

        return resVal;
    }



    private bool ParseNodes(string line, int lineIndex, out string msg)
    {
        char lastChar = ' ';
        char nextChar;
        msg = "";
        var actualLineIndex = lineIndex + 1;
        var word = string.Empty;
        var token = new Token();
        var ignoreCnt = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if(ignoreCnt > 0)
            {
                ignoreCnt--;
                continue;
            }
            nextChar = line[i];
            if (nextChar == ' ')
                continue;
            if (!IsVaildChar(nextChar))
            {
                msg = $"char:{nextChar} is invaild at position:{i + 1} line:{actualLineIndex}";
                return false;
            }
            if (i == 0)
            {
                if(!CheckFirstChar(nextChar, line.Length))
                {
                    msg = $"char:{nextChar} is invaild at position:{i + 1} line:{actualLineIndex}";
                    return false;
                }
                
                // 解析加入语法树
                if (!ParseNode(nextChar, lastChar, ref word,ref token, out msg))
                {
                    msg = $"char:{nextChar} is invaild at position:{i + 1} line:{actualLineIndex}";
                    return false;
                }
                lastChar = nextChar;
                word += nextChar;
                continue;
            }
            else if (!CheckSyntaxBetweenTwoChars(nextChar, lastChar, i, line.Length))
            {
                if (GetFunction(lastChar, nextChar, line, i,out var funcName))
                {
                    var temp = new Token(token);
                    temp.Content = funcName;
                    temp.Type = TokenTypes.Function;
                    token = temp;
                    ignoreCnt = funcName.Length;
                    continue;
                }
                msg = $"char:{nextChar} is invaild at position:{i + 1} line:{actualLineIndex}";
                return false;
            }
            
            // 解析加入语法树
            if (!ParseNode(nextChar, lastChar, ref word,ref token, out msg))
            {
                msg = $"char:{nextChar} is invaild at position:{i + 1} line:{actualLineIndex}";
                return false;
            }
            lastChar = nextChar;
            if(!IsBrace(nextChar))
            {
                word += nextChar;
            }
        }
        while (token.Parent != null)
        {
            token = token.Parent;
        }
        _tokens = token;
        return true;
    }

    private static bool GetFunction(char lastChar, char nextChar, string line, int i,out string functionName)
    {
        functionName = string.Empty;
        if (!IsOperator(lastChar) || !IsAlpahbet(nextChar))
        {
            return false;
        }
        var tempStr = line.Substring(i);
        var charList = new List<char>();
        foreach (char c in tempStr)
        {
            if(c == '(')
            {
                functionName = string.Concat(charList);
                return true;
            }
            else if(!IsAlpahbet(c))
            {
                return false;
            }
            charList.Add(c);
        }
        return false;
    }

    private static bool ParseNode(char next, char last, ref string word,ref Token token, out string msg)
    {
        msg = "";
        Token temp = null;
        if (next == '(')
        {
            temp = new Token(token);
            token.Type = TokenTypes.Brace;
            token = temp;
            if(IsAlpahbet(last))
            {
                token.Content = word;
                token.Type = TokenTypes.Function;
                var child = new Token(token);
                token.Tokens.Add(child);
                token = child;
                word = string.Empty;
            }
        }
        else if (next == '[')
        {
            temp = new Token(token);
            temp.Type = TokenTypes.Variable;
            token = temp;
            word = string.Empty;
        }
        else if (next == ']')
        {
            token.Content = word;
            token = token.Parent;
            word = string.Empty;
        }
        else if(IsOperator(next))
        {
            if(IsNumber(last))
            {
                temp = new Token(token);
                temp.Content = word;
                temp.Type = TokenTypes.Const;
            }
            temp = new Token(token);
            temp.Type = TokenTypes.Operator;
            temp.Content = next.ToString();
            word = string.Empty;
        }
        else if(IsNumber(next))
        {
            
        }
        return true;
    }

    private static bool CheckSyntaxBetweenTwoChars(char ch,char lastCh, int charIndex, int strLen)
    {
        if(charIndex == strLen - 1 && !IsNumber(ch) && ch != ']' && ch != ')')
        {
            return false;
        }
        else if (!CheckBracket(ch, lastCh))
        {
            return false;
        }
        else if (!CheckSign(ch, lastCh))
        {
            return false;
        }
        return true;
    }

    private static bool CheckBracket(char next,char last)
    {
        if(next == ')')
        {
            if (last == '(')
                return false;
            if (IsAlpahbet(last))
                return false;
        }
        else if(last == ')')
        {
            switch (next)
            {
                case '(':
                case '[':
                    return false;
            }
        }
        else if(last == '[')
        {
            if (IsNumber(last))
            {
                return false;
            }
            else if(!IsAlpahbet(next))
            {
                return false;
            }
        }
        return true;
    }

    private static bool CheckSign(char nextChar, char lastChar)
    {
        if((IsOperator(lastChar) && IsOperator(nextChar)) || (lastChar == '.' && nextChar == '.'))
        {
            return false;
        }
        if(lastChar == '[' && IsNumber(nextChar))
        {
            return false;
        }
        else if((IsOperator(lastChar) && IsAlpahbet(nextChar)) || (IsAlpahbet(lastChar) && IsOperator(nextChar)))
        {
            return false;
        }
        else if((lastChar == ')' || lastChar == ']') && (IsNumber(nextChar) || IsAlpahbet(nextChar)))
        {
            return false;
        }
        return true;
    }

    private static bool IsVaildChar(char c)
    {
        // ASCII码对照
        if (c >= 40 && c <= 57 && c != 44)
            return true;
        else if (c >= 65 && c <= 122 &&
            c != 92 && c != 94 && c != 95 && c != 96)
            return true;
        return false;
    }

    private static bool IsNumber(char c)
    {
        // ASCII码对照
        return c >= 48 && c <= 57;
    }

    private static bool IsOperator(char c)
    {
        return operators.Contains(c);
    }

    private static bool IsAlpahbet(char c)
    {
        return (c >= 65 && c <= 90) || (c >= 97 && c <= 122);
    }

    private static bool IsBrace(char c)
    {
        return c == ']' || c == '[' || c == ')' || c == '(';
    }

    private static bool CheckFirstChar(char ch, int lineLength)
    {
        switch (ch)
        {
            case '+':
            case '/':
            case '*':
            case ']':
            case ')':
                return false;
            case '(':
            case '[':
                if (lineLength <= 2)
                {
                    return false;
                }
                break;
        }
        return true;
    }

    #endregion

    private string _buffer;
    private Token _tokens = new Token();
    private static readonly char[] numbers = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
    private static readonly char[] operators = new char[] { '+', '-', '*', '/' };
    private static readonly Dictionary<string, Func<object[],object>> functions = new Dictionary<string, Func<object[], object>>
    {
        {"abs",ps => Math.Abs(Convert.ToDecimal(ps[0])) }
    };

    public class Token : IEnumerable<Token>
    {
        public Token(Token parent = null)
        {
            Parent = parent;
            Parent?.Tokens?.Add(this);
        }

        public TokenTypes Type { get; internal set; }

        public string Content { get; internal set; }

        public Token Parent { get; internal set; }

        public List<Token> Tokens { get; } = new List<Token>();

        public override string ToString()
        {
            return $"[Type:{Type},Value:{Content}]";
        }

        public IEnumerator<Token> GetEnumerator()
        {
            return Tokens.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Tokens).GetEnumerator();
        }
    }
}

public static class Extensions
{
    public static string AsString(this IEnumerable<char> buffer)
    {
        return string.Concat(buffer);
    }
}
