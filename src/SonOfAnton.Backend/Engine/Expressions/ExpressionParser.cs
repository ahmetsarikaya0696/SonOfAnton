namespace SonOfAnton.Backend.Engine.Expressions;

// ─── AST Node Tipleri ──────────────────────────────────────────

/// <summary>
/// Soyut sözdizim ağacı düğümü (base)
/// </summary>
public abstract class AstNode { }

/// <summary>
/// Sabit değer: "hello", 42, true, null
/// </summary>
public class LiteralNode : AstNode
{
    public object? Value { get; init; }
}

/// <summary>
/// Değişken referansı: myVar, $item, $index
/// </summary>
public class VariableNode : AstNode
{
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Fonksiyon çağrısı: formatDate(now(); "yyyy-MM-dd")
/// </summary>
public class FunctionCallNode : AstNode
{
    public string FunctionName { get; init; } = string.Empty;
    public List<AstNode> Arguments { get; init; } = [];
}

/// <summary>
/// Nokta ile erişim: obj.field.sub
/// </summary>
public class PropertyAccessNode : AstNode
{
    public AstNode Object { get; init; } = null!;
    public string Property { get; init; } = string.Empty;
}

/// <summary>
/// İkili operatör: a + b, x > 5, a == b
/// </summary>
public class BinaryOpNode : AstNode
{
    public AstNode Left { get; init; } = null!;
    public string Operator { get; init; } = string.Empty;
    public AstNode Right { get; init; } = null!;
}

/// <summary>
/// Tekli operatör: !x, -5
/// </summary>
public class UnaryOpNode : AstNode
{
    public string Operator { get; init; } = string.Empty;
    public AstNode Operand { get; init; } = null!;
}

/// <summary>
/// Dizin erişimi: arr[0]
/// </summary>
public class IndexAccessNode : AstNode
{
    public AstNode Object { get; init; } = null!;
    public AstNode Index { get; init; } = null!;
}

// ─── Parser ────────────────────────────────────────────────────

/// <summary>
/// Recursive descent parser — tokenlardan AST oluşturur
/// Öncelik sırası (düşükten yükseğe):
///   OR → AND → Equality → Comparison → Addition → Multiplication → Unary → Postfix → Primary
/// </summary>
public class ExpressionParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public ExpressionParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public AstNode Parse()
    {
        var node = ParseOr();
        return node;
    }

    /// <summary>
    /// İfadeyi doğrulama modunda parse eder — hata varsa fırlatır
    /// </summary>
    public static void Validate(string expression)
    {
        var lexer = new ExpressionLexer(expression);
        var tokens = lexer.Tokenize();
        var parser = new ExpressionParser(tokens);
        parser.Parse();
    }

    // ─── Precedence Levels ─────────────────────────────────

    private AstNode ParseOr()
    {
        var left = ParseAnd();

        while (Current.Type == TokenType.Or)
        {
            Advance();
            var right = ParseAnd();
            left = new BinaryOpNode { Left = left, Operator = "||", Right = right };
        }

        return left;
    }

    private AstNode ParseAnd()
    {
        var left = ParseEquality();

        while (Current.Type == TokenType.And)
        {
            Advance();
            var right = ParseEquality();
            left = new BinaryOpNode { Left = left, Operator = "&&", Right = right };
        }

        return left;
    }

    private AstNode ParseEquality()
    {
        var left = ParseComparison();

        while (Current.Type is TokenType.Equal or TokenType.NotEqual)
        {
            var op = Current.Value;
            Advance();
            var right = ParseComparison();
            left = new BinaryOpNode { Left = left, Operator = op, Right = right };
        }

        return left;
    }

    private AstNode ParseComparison()
    {
        var left = ParseAddition();

        while (Current.Type is TokenType.LessThan or TokenType.GreaterThan
               or TokenType.LessEqual or TokenType.GreaterEqual)
        {
            var op = Current.Value;
            Advance();
            var right = ParseAddition();
            left = new BinaryOpNode { Left = left, Operator = op, Right = right };
        }

        return left;
    }

    private AstNode ParseAddition()
    {
        var left = ParseMultiplication();

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Current.Value;
            Advance();
            var right = ParseMultiplication();
            left = new BinaryOpNode { Left = left, Operator = op, Right = right };
        }

        return left;
    }

    private AstNode ParseMultiplication()
    {
        var left = ParseUnary();

        while (Current.Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = Current.Value;
            Advance();
            var right = ParseUnary();
            left = new BinaryOpNode { Left = left, Operator = op, Right = right };
        }

        return left;
    }

    private AstNode ParseUnary()
    {
        if (Current.Type == TokenType.Not)
        {
            Advance();
            var operand = ParseUnary();
            return new UnaryOpNode { Operator = "!", Operand = operand };
        }

        if (Current.Type == TokenType.Minus)
        {
            Advance();
            var operand = ParseUnary();
            return new UnaryOpNode { Operator = "-", Operand = operand };
        }

        return ParsePostfix();
    }

    private AstNode ParsePostfix()
    {
        var node = ParsePrimary();

        while (true)
        {
            if (Current.Type == TokenType.Dot)
            {
                Advance();
                if (Current.Type != TokenType.Identifier)
                    throw new ExpressionParseException("'.' sonrası identifier bekleniyor", Current.Position);

                var prop = Current.Value;
                Advance();

                // Metot çağrısı mı? obj.method()
                if (Current.Type == TokenType.LeftParen)
                {
                    var args = ParseArguments();
                    args.Insert(0, node);
                    node = new FunctionCallNode { FunctionName = prop, Arguments = args };
                }
                else
                {
                    node = new PropertyAccessNode { Object = node, Property = prop };
                }
            }
            else if (Current.Type == TokenType.LeftBracket)
            {
                Advance();
                var index = ParseOr();
                Expect(TokenType.RightBracket, "']' bekleniyor");
                node = new IndexAccessNode { Object = node, Index = index };
            }
            else
            {
                break;
            }
        }

        return node;
    }

    private AstNode ParsePrimary()
    {
        var token = Current;

        switch (token.Type)
        {
            case TokenType.NumberLiteral:
                Advance();
                if (token.Value.Contains('.'))
                    return new LiteralNode { Value = double.Parse(token.Value, System.Globalization.CultureInfo.InvariantCulture) };
                return new LiteralNode { Value = long.Parse(token.Value) };

            case TokenType.StringLiteral:
                Advance();
                return new LiteralNode { Value = token.Value };

            case TokenType.BoolLiteral:
                Advance();
                return new LiteralNode { Value = token.Value.Equals("true", StringComparison.OrdinalIgnoreCase) };

            case TokenType.NullLiteral:
                Advance();
                return new LiteralNode { Value = null };

            case TokenType.Identifier:
                return ParseIdentifierOrCall();

            case TokenType.LeftParen:
                Advance();
                var expr = ParseOr();
                Expect(TokenType.RightParen, "')' bekleniyor");
                return expr;

            default:
                throw new ExpressionParseException($"Beklenmeyen token: {token}", token.Position);
        }
    }

    private AstNode ParseIdentifierOrCall()
    {
        var name = Current.Value;
        Advance();

        // Fonksiyon çağrısı mı?
        if (Current.Type == TokenType.LeftParen)
        {
            var args = ParseArguments();
            return new FunctionCallNode { FunctionName = name, Arguments = args };
        }

        // $ ile başlayan özel değişken (ör: $env, $node gibi)
        if (name.StartsWith('$'))
        {
            var fullName = name;
            while (Current.Type == TokenType.Dot)
            {
                Advance();
                if (Current.Type != TokenType.Identifier && Current.Type != TokenType.NumberLiteral)
                    break;
                fullName += "." + Current.Value;
                Advance();
            }
            return new VariableNode { Name = fullName };
        }

        // Normal değişken
        return new VariableNode { Name = name };
    }

    private List<AstNode> ParseArguments()
    {
        Expect(TokenType.LeftParen, "'(' bekleniyor");
        var args = new List<AstNode>();

        if (Current.Type != TokenType.RightParen)
        {
            args.Add(ParseOr());

            while (Current.Type is TokenType.Semicolon or TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightParen) break; // trailing separator
                args.Add(ParseOr());
            }
        }

        Expect(TokenType.RightParen, "')' bekleniyor");
        return args;
    }

    // ─── Helpers ───────────────────────────────────────────

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private void Advance() => _pos++;

    private void Expect(TokenType type, string errorMessage)
    {
        if (Current.Type != type)
            throw new ExpressionParseException(errorMessage, Current.Position);
        Advance();
    }
}

/// <summary>
/// İfade parse hatası
/// </summary>
public class ExpressionParseException : Exception
{
    public int Position { get; }

    public ExpressionParseException(string message, int position)
        : base($"İfade hatası (konum {position}): {message}")
    {
        Position = position;
    }
}
