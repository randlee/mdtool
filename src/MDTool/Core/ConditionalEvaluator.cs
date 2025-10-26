using System.Text;
using System.Text.RegularExpressions;
using MDTool.Models;

namespace MDTool.Core;

/// <summary>
/// Evaluates conditional blocks in markdown content.
/// Supports {{#if}}, {{else if}}, {{else}}, and {{/if}} tags with boolean expressions.
/// </summary>
public class ConditionalEvaluator
{
    private readonly IArgsAccessor _args;
    private readonly ConditionalOptions _options;
    private readonly ConditionalTrace _trace;

    private ConditionalEvaluator(IArgsAccessor args, ConditionalOptions options)
    {
        _args = args ?? throw new ArgumentNullException(nameof(args));
        _options = options ?? new ConditionalOptions();
        _trace = new ConditionalTrace();
    }

    /// <summary>
    /// Evaluates {{#if}} blocks against the provided args and returns pruned content.
    /// </summary>
    /// <param name="content">Original markdown content</param>
    /// <param name="args">Case-insensitive args view (JSON-backed)</param>
    /// <param name="options">Evaluation options</param>
    /// <returns>ProcessingResult with pruned content or errors</returns>
    public static ProcessingResult<string> Evaluate(
        string content,
        IArgsAccessor args,
        ConditionalOptions? options = null)
    {
        var evaluator = new ConditionalEvaluator(args, options ?? new ConditionalOptions());
        return evaluator.EvaluateInternal(content);
    }

    /// <summary>
    /// Evaluates conditionals and returns both content and trace.
    /// </summary>
    public static ProcessingResult<(string content, ConditionalTrace trace)> EvaluateDetailed(
        string content,
        IArgsAccessor args,
        ConditionalOptions? options = null)
    {
        var evaluator = new ConditionalEvaluator(args, options ?? new ConditionalOptions());
        var result = evaluator.EvaluateInternal(content);

        if (!result.Success)
        {
            return ProcessingResult<(string, ConditionalTrace)>.Fail(result.Errors);
        }

        return ProcessingResult<(string, ConditionalTrace)>.Ok((result.Value!, evaluator._trace));
    }

    private ProcessingResult<string> EvaluateInternal(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ProcessingResult<string>.Ok(content ?? "");
        }

        try
        {
            var blocks = ParseBlocks(content);
            var result = ProcessBlocks(content, blocks);
            return result;
        }
        catch (Exception ex)
        {
            return ProcessingResult<string>.Fail(
                ValidationError.ProcessingError($"Conditional evaluation failed: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Parses all conditional blocks from the content.
    /// </summary>
    private List<ConditionalBlock> ParseBlocks(string content)
    {
        var blocks = new List<ConditionalBlock>();
        var lines = content.Split('\n');
        var stack = new Stack<ConditionalBlock>();
        var inCodeFence = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Track code fences (skip tags inside code blocks)
            if (IsCodeFenceToggle(line))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence)
            {
                continue;
            }

            // Check for conditional tags
            if (TryParseIfTag(line, out var expr))
            {
                if (stack.Count >= _options.MaxNesting)
                {
                    throw new InvalidOperationException(
                        $"Maximum nesting depth exceeded ({_options.MaxNesting}) at line {lineNumber}"
                    );
                }

                var block = new ConditionalBlock
                {
                    StartLine = lineNumber,
                    Branches = new List<ConditionalBranch>
                    {
                        new() { Kind = "if", Expression = expr, StartLine = lineNumber }
                    }
                };
                stack.Push(block);
            }
            else if (TryParseElseIfTag(line, out expr))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException($"{{{{else if}}}} without matching {{{{#if}}}} at line {lineNumber}");
                }

                var block = stack.Peek();
                var lastBranch = block.Branches[^1];
                lastBranch.EndLine = lineNumber - 1;

                block.Branches.Add(new ConditionalBranch
                {
                    Kind = "else-if",
                    Expression = expr,
                    StartLine = lineNumber
                });
            }
            else if (TryParseElseTag(line))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException($"{{{{else}}}} without matching {{{{#if}}}} at line {lineNumber}");
                }

                var block = stack.Peek();
                var lastBranch = block.Branches[^1];
                lastBranch.EndLine = lineNumber - 1;

                block.Branches.Add(new ConditionalBranch
                {
                    Kind = "else",
                    Expression = null,
                    StartLine = lineNumber
                });
            }
            else if (TryParseEndIfTag(line))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException($"{{{{/if}}}} without matching {{{{#if}}}} at line {lineNumber}");
                }

                var block = stack.Pop();
                block.EndLine = lineNumber;

                var lastBranch = block.Branches[^1];
                lastBranch.EndLine = lineNumber - 1;

                blocks.Add(block);
            }
        }

        if (stack.Count > 0)
        {
            var unclosed = stack.Peek();
            throw new InvalidOperationException($"Unclosed {{{{#if}}}} starting at line {unclosed.StartLine}");
        }

        return blocks;
    }

    /// <summary>
    /// Processes blocks and returns the pruned content.
    /// </summary>
    private ProcessingResult<string> ProcessBlocks(string content, List<ConditionalBlock> blocks)
    {
        var lines = content.Split('\n');
        var result = new StringBuilder();
        var skipSet = new HashSet<int>();

        foreach (var block in blocks)
        {
            var blockTrace = new ConditionalBlockTrace
            {
                StartLine = block.StartLine,
                EndLine = block.EndLine
            };

            bool branchTaken = false;
            (int start, int end)? keepRange = null;

            foreach (var branch in block.Branches)
            {
                var taken = false;

                if (!branchTaken)
                {
                    if (branch.Kind == "else")
                    {
                        taken = true;
                    }
                    else
                    {
                        var evalResult = EvaluateExpression(branch.Expression!);
                        if (!evalResult.Success)
                        {
                            return ProcessingResult<string>.Fail(evalResult.Errors);
                        }

                        taken = evalResult.Value;
                    }

                    if (taken)
                    {
                        branchTaken = true;
                        keepRange = (branch.StartLine, branch.EndLine);
                    }
                }

                blockTrace.Branches.Add(new ConditionalBranchTrace
                {
                    Kind = branch.Kind,
                    Expr = branch.Expression,
                    Taken = taken
                });
            }

            _trace.Blocks.Add(blockTrace);

            // Skip all tag lines and non-taken branch content
            // Keep only content lines from the taken branch
            for (var i = block.StartLine; i <= block.EndLine; i++)
            {
                // Keep content lines from taken branch (not the tag line itself)
                if (keepRange.HasValue && i > keepRange.Value.start && i <= keepRange.Value.end)
                {
                    // This is a content line in the taken branch - keep it
                    continue;
                }

                // Skip this line (it's a tag or in a non-taken branch)
                skipSet.Add(i);
            }
        }

        // Build result by including only non-skipped lines
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            if (!skipSet.Contains(lineNumber))
            {
                if (result.Length > 0)
                {
                    result.Append('\n');
                }
                result.Append(lines[i]);
            }
        }

        return ProcessingResult<string>.Ok(result.ToString());
    }

    /// <summary>
    /// Evaluates a boolean expression.
    /// </summary>
    private ProcessingResult<bool> EvaluateExpression(string expression)
    {
        try
        {
            var tokens = Tokenize(expression);
            var value = ParseExpression(tokens);
            return ProcessingResult<bool>.Ok(value);
        }
        catch (Exception ex)
        {
            return ProcessingResult<bool>.Fail(
                ValidationError.ProcessingError($"Expression evaluation error: {ex.Message}")
            );
        }
    }

    /// <summary>
    /// Tokenizes an expression into a list of tokens.
    /// </summary>
    private List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < expression.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(expression[i]))
            {
                i++;
                continue;
            }

            // Operators
            if (i + 1 < expression.Length && expression.Substring(i, 2) == "==")
            {
                tokens.Add(new Token { Type = TokenType.Equals, Value = "==" });
                i += 2;
            }
            else if (i + 1 < expression.Length && expression.Substring(i, 2) == "!=")
            {
                tokens.Add(new Token { Type = TokenType.NotEquals, Value = "!=" });
                i += 2;
            }
            else if (i + 1 < expression.Length && expression.Substring(i, 2) == "&&")
            {
                tokens.Add(new Token { Type = TokenType.And, Value = "&&" });
                i += 2;
            }
            else if (i + 1 < expression.Length && expression.Substring(i, 2) == "||")
            {
                tokens.Add(new Token { Type = TokenType.Or, Value = "||" });
                i += 2;
            }
            else if (expression[i] == '!')
            {
                tokens.Add(new Token { Type = TokenType.Not, Value = "!" });
                i++;
            }
            else if (expression[i] == '(')
            {
                tokens.Add(new Token { Type = TokenType.LeftParen, Value = "(" });
                i++;
            }
            else if (expression[i] == ')')
            {
                tokens.Add(new Token { Type = TokenType.RightParen, Value = ")" });
                i++;
            }
            else if (expression[i] == ',')
            {
                tokens.Add(new Token { Type = TokenType.Comma, Value = "," });
                i++;
            }
            else if (expression[i] == '[')
            {
                tokens.Add(new Token { Type = TokenType.LeftBracket, Value = "[" });
                i++;
            }
            else if (expression[i] == ']')
            {
                tokens.Add(new Token { Type = TokenType.RightBracket, Value = "]" });
                i++;
            }
            else if (expression[i] == '.')
            {
                tokens.Add(new Token { Type = TokenType.Dot, Value = "." });
                i++;
            }
            // String literals
            else if (expression[i] == '"' || expression[i] == '\'')
            {
                var quote = expression[i];
                var sb = new StringBuilder();
                i++;

                while (i < expression.Length && expression[i] != quote)
                {
                    sb.Append(expression[i]);
                    i++;
                }

                if (i >= expression.Length)
                {
                    throw new InvalidOperationException($"Unterminated string literal");
                }

                i++; // Skip closing quote
                tokens.Add(new Token { Type = TokenType.String, Value = sb.ToString() });
            }
            // Numbers
            else if (char.IsDigit(expression[i]) || (expression[i] == '-' && i + 1 < expression.Length && char.IsDigit(expression[i + 1])))
            {
                var sb = new StringBuilder();
                if (expression[i] == '-')
                {
                    sb.Append(expression[i]);
                    i++;
                }

                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    sb.Append(expression[i]);
                    i++;
                }

                tokens.Add(new Token { Type = TokenType.Number, Value = sb.ToString() });
            }
            // Identifiers (variables, functions, keywords)
            else if (char.IsLetter(expression[i]) || expression[i] == '_')
            {
                var sb = new StringBuilder();

                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    sb.Append(expression[i]);
                    i++;
                }

                var identifier = sb.ToString();

                // Check for keywords
                if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new Token { Type = TokenType.Boolean, Value = "true" });
                }
                else if (identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Add(new Token { Type = TokenType.Boolean, Value = "false" });
                }
                else
                {
                    tokens.Add(new Token { Type = TokenType.Identifier, Value = identifier });
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected character: {expression[i]}");
            }
        }

        return tokens;
    }

    /// <summary>
    /// Parses and evaluates an expression using recursive descent.
    /// Operator precedence: ! > && > ||
    /// </summary>
    private bool ParseExpression(List<Token> tokens)
    {
        var pos = 0;
        return ParseOr(tokens, ref pos);
    }

    private bool ParseOr(List<Token> tokens, ref int pos)
    {
        var left = ParseAnd(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Type == TokenType.Or)
        {
            pos++; // consume ||
            var right = ParseAnd(tokens, ref pos);
            left = left || right;
        }

        return left;
    }

    private bool ParseAnd(List<Token> tokens, ref int pos)
    {
        var left = ParseUnary(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Type == TokenType.And)
        {
            pos++; // consume &&
            var right = ParseUnary(tokens, ref pos);
            left = left && right;
        }

        return left;
    }

    private bool ParseUnary(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Type == TokenType.Not)
        {
            pos++; // consume !
            return !ParseUnary(tokens, ref pos);
        }

        return ParseComparison(tokens, ref pos);
    }

    private bool ParseComparison(List<Token> tokens, ref int pos)
    {
        var left = ParsePrimary(tokens, ref pos);

        if (pos < tokens.Count && (tokens[pos].Type == TokenType.Equals || tokens[pos].Type == TokenType.NotEquals))
        {
            var op = tokens[pos].Type;
            pos++; // consume operator
            var right = ParsePrimary(tokens, ref pos);

            return CompareValues(left, right, op);
        }

        // No comparison operator, treat as boolean
        return ToBool(left);
    }

    private object ParsePrimary(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
        {
            throw new InvalidOperationException("Unexpected end of expression");
        }

        var token = tokens[pos];

        // Parentheses
        if (token.Type == TokenType.LeftParen)
        {
            pos++; // consume (
            var value = ParseOr(tokens, ref pos);

            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightParen)
            {
                throw new InvalidOperationException("Missing closing parenthesis");
            }

            pos++; // consume )
            return value;
        }

        // Literals
        if (token.Type == TokenType.String)
        {
            pos++;
            return token.Value;
        }

        if (token.Type == TokenType.Number)
        {
            pos++;
            if (token.Value.Contains('.'))
            {
                return double.Parse(token.Value);
            }
            return long.Parse(token.Value);
        }

        if (token.Type == TokenType.Boolean)
        {
            pos++;
            return bool.Parse(token.Value);
        }

        // Identifiers (variables or functions)
        if (token.Type == TokenType.Identifier)
        {
            var identifier = token.Value;
            pos++;

            // Check for function call or method call
            if (pos < tokens.Count && tokens[pos].Type == TokenType.LeftParen)
            {
                return ParseFunctionCall(identifier, tokens, ref pos);
            }

            // Check for dot notation (property access or method call)
            if (pos < tokens.Count && tokens[pos].Type == TokenType.Dot)
            {
                pos++; // consume .

                if (pos >= tokens.Count || tokens[pos].Type != TokenType.Identifier)
                {
                    throw new InvalidOperationException("Expected method name after dot");
                }

                var methodName = tokens[pos].Value;
                pos++;

                if (pos < tokens.Count && tokens[pos].Type == TokenType.LeftParen)
                {
                    return ParseMethodCall(identifier, methodName, tokens, ref pos);
                }

                // Property access (e.g., USER.NAME)
                var fullPath = $"{identifier}.{methodName}";
                return ResolveVariable(fullPath);
            }

            // Simple variable
            return ResolveVariable(identifier);
        }

        throw new InvalidOperationException($"Unexpected token: {token.Type}");
    }

    private object ParseFunctionCall(string functionName, List<Token> tokens, ref int pos)
    {
        pos++; // consume (

        var args = new List<object>();

        while (pos < tokens.Count && tokens[pos].Type != TokenType.RightParen)
        {
            if (tokens[pos].Type == TokenType.LeftBracket)
            {
                // Array literal
                args.Add(ParseArrayLiteral(tokens, ref pos));
            }
            else
            {
                args.Add(ParsePrimary(tokens, ref pos));
            }

            if (pos < tokens.Count && tokens[pos].Type == TokenType.Comma)
            {
                pos++; // consume ,
            }
        }

        if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightParen)
        {
            throw new InvalidOperationException("Missing closing parenthesis in function call");
        }

        pos++; // consume )

        return EvaluateFunction(functionName, args);
    }

    private object ParseMethodCall(string objectName, string methodName, List<Token> tokens, ref int pos)
    {
        pos++; // consume (

        var args = new List<object> { ResolveVariable(objectName) };

        while (pos < tokens.Count && tokens[pos].Type != TokenType.RightParen)
        {
            args.Add(ParsePrimary(tokens, ref pos));

            if (pos < tokens.Count && tokens[pos].Type == TokenType.Comma)
            {
                pos++; // consume ,
            }
        }

        if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightParen)
        {
            throw new InvalidOperationException("Missing closing parenthesis in method call");
        }

        pos++; // consume )

        return EvaluateFunction(methodName, args);
    }

    private List<object> ParseArrayLiteral(List<Token> tokens, ref int pos)
    {
        pos++; // consume [

        var items = new List<object>();

        while (pos < tokens.Count && tokens[pos].Type != TokenType.RightBracket)
        {
            items.Add(ParsePrimary(tokens, ref pos));

            if (pos < tokens.Count && tokens[pos].Type == TokenType.Comma)
            {
                pos++; // consume ,
            }
        }

        if (pos >= tokens.Count || tokens[pos].Type != TokenType.RightBracket)
        {
            throw new InvalidOperationException("Missing closing bracket in array literal");
        }

        pos++; // consume ]

        return items;
    }

    private object ResolveVariable(string name)
    {
        if (_args.TryGet(name, out var value))
        {
            return value!;
        }

        if (_options.Strict)
        {
            throw new InvalidOperationException($"Unknown variable: {name}");
        }

        return false;
    }

    private object EvaluateFunction(string name, List<object> args)
    {
        var normalizedName = name.ToLowerInvariant();

        switch (normalizedName)
        {
            case "contains":
                if (args.Count != 2)
                    throw new InvalidOperationException("contains() requires 2 arguments");
                return Contains(args[0], args[1]);

            case "startswith":
                if (args.Count != 2)
                    throw new InvalidOperationException("startsWith() requires 2 arguments");
                return StartsWith(args[0], args[1]);

            case "endswith":
                if (args.Count != 2)
                    throw new InvalidOperationException("endsWith() requires 2 arguments");
                return EndsWith(args[0], args[1]);

            case "in":
                if (args.Count != 2)
                    throw new InvalidOperationException("in() requires 2 arguments");
                return In(args[0], args[1]);

            case "exists":
                if (args.Count != 1)
                    throw new InvalidOperationException("exists() requires 1 argument");
                return Exists(args[0]);

            default:
                throw new InvalidOperationException($"Unknown function: {name}");
        }
    }

    private bool Contains(object haystack, object needle)
    {
        var haystackStr = ToString(haystack);
        var needleStr = ToString(needle);

        var comparison = _options.CaseSensitiveStrings
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return haystackStr.Contains(needleStr, comparison);
    }

    private bool StartsWith(object text, object prefix)
    {
        var textStr = ToString(text);
        var prefixStr = ToString(prefix);

        var comparison = _options.CaseSensitiveStrings
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return textStr.StartsWith(prefixStr, comparison);
    }

    private bool EndsWith(object text, object suffix)
    {
        var textStr = ToString(text);
        var suffixStr = ToString(suffix);

        var comparison = _options.CaseSensitiveStrings
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return textStr.EndsWith(suffixStr, comparison);
    }

    private bool In(object value, object array)
    {
        if (array is not List<object> list)
        {
            throw new InvalidOperationException("Second argument to in() must be an array");
        }

        var comparison = _options.CaseSensitiveStrings
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        foreach (var item in list)
        {
            if (CompareValues(value, item, TokenType.Equals))
            {
                return true;
            }
        }

        return false;
    }

    private bool Exists(object value)
    {
        // If we got here, the variable was resolved
        // In ResolveVariable, unknown vars return false in non-strict mode
        // So if value is bool false, it might be unknown or actually false
        // We need to check if it's a real value
        return value is not bool b || b;
    }

    private bool CompareValues(object left, object right, TokenType op)
    {
        // Type-aware comparison
        var leftType = GetValueType(left);
        var rightType = GetValueType(right);

        // Type mismatch
        if (leftType != rightType)
        {
            if (_options.Strict)
            {
                throw new InvalidOperationException($"Type mismatch in comparison: {leftType} vs {rightType}");
            }

            return op == TokenType.NotEquals;
        }

        bool equal;

        if (leftType == ValueType.String)
        {
            var leftStr = ToString(left);
            var rightStr = ToString(right);

            var comparison = _options.CaseSensitiveStrings
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            equal = string.Equals(leftStr, rightStr, comparison);
        }
        else if (leftType == ValueType.Number)
        {
            var leftNum = ToNumber(left);
            var rightNum = ToNumber(right);
            equal = Math.Abs(leftNum - rightNum) < 0.0000001;
        }
        else if (leftType == ValueType.Boolean)
        {
            equal = ToBool(left) == ToBool(right);
        }
        else
        {
            equal = false;
        }

        return op == TokenType.Equals ? equal : !equal;
    }

    private ValueType GetValueType(object value)
    {
        if (value is string)
            return ValueType.String;
        if (value is bool)
            return ValueType.Boolean;
        if (value is long || value is int || value is double || value is float)
            return ValueType.Number;

        return ValueType.Unknown;
    }

    private string ToString(object value)
    {
        return value?.ToString() ?? "";
    }

    private double ToNumber(object value)
    {
        return value switch
        {
            long l => l,
            int i => i,
            double d => d,
            float f => f,
            string s when double.TryParse(s, out var d) => d,
            _ => 0
        };
    }

    private bool ToBool(object value)
    {
        return value switch
        {
            bool b => b,
            string s => !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase),
            long l => l != 0,
            int i => i != 0,
            double d => Math.Abs(d) > 0.0000001,
            _ => false
        };
    }

    private bool IsCodeFenceToggle(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```") || trimmed.StartsWith("~~~");
    }

    private bool TryParseIfTag(string line, out string expression)
    {
        var match = Regex.Match(line, @"{{\s*#if\s+(.+?)\s*}}", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            expression = match.Groups[1].Value.Trim();
            return true;
        }

        expression = "";
        return false;
    }

    private bool TryParseElseIfTag(string line, out string expression)
    {
        var match = Regex.Match(line, @"{{\s*else\s+if\s+(.+?)\s*}}", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            expression = match.Groups[1].Value.Trim();
            return true;
        }

        expression = "";
        return false;
    }

    private bool TryParseElseTag(string line)
    {
        return Regex.IsMatch(line, @"{{\s*else\s*}}", RegexOptions.IgnoreCase);
    }

    private bool TryParseEndIfTag(string line)
    {
        return Regex.IsMatch(line, @"{{\s*/if\s*}}", RegexOptions.IgnoreCase);
    }

    private enum TokenType
    {
        Identifier,
        String,
        Number,
        Boolean,
        Equals,
        NotEquals,
        And,
        Or,
        Not,
        LeftParen,
        RightParen,
        Comma,
        LeftBracket,
        RightBracket,
        Dot
    }

    private class Token
    {
        public TokenType Type { get; init; }
        public string Value { get; init; } = "";
    }

    private enum ValueType
    {
        String,
        Number,
        Boolean,
        Unknown
    }

    private class ConditionalBlock
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public List<ConditionalBranch> Branches { get; set; } = new();
    }

    private class ConditionalBranch
    {
        public string Kind { get; set; } = "";
        public string? Expression { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }
    }
}
