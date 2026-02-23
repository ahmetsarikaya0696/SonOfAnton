namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// Token tipleri
/// </summary>
public enum TokenType
{
    // Literals
    StringLiteral,
    NumberLiteral,
    BoolLiteral,
    NullLiteral,

    // Identifiers
    Identifier,

    // Delimiters
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Semicolon,
    Comma,
    Dot,

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,

    // Comparison
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessEqual,
    GreaterEqual,

    // Logical
    And,
    Or,
    Not,

    // Special
    Dollar,
    Colon,
    EOF
}

/// <summary>
/// Lexer token
/// </summary>
public class Token
{
    public TokenType Type { get; init; }
    public string Value { get; init; } = string.Empty;
    public int Position { get; init; }

    public override string ToString() => $"[{Type}: {Value}]";
}

/// <summary>
/// Expression tokenizer — ifadeyi tokenlara ayırır
/// Make.com uyumlu sözdizimi: fonksiyon argümanları ; ile ayrılır
/// </summary>
public class ExpressionLexer
{
    private readonly string _source;
    private int _pos;

    public ExpressionLexer(string source)
    {
        _source = source;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _source.Length)
        {
            SkipWhitespace();
            if (_pos >= _source.Length) break;

            var ch = _source[_pos];

            // String literal — çift veya tek tırnak
            if (ch is '"' or '\'')
            {
                tokens.Add(ReadString(ch));
                continue;
            }

            // Number literal
            if (char.IsDigit(ch) || (ch == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            // Identifier veya keyword
            if (char.IsLetter(ch) || ch == '_' || ch == '$')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            // İki karakterli operatörler
            if (_pos + 1 < _source.Length)
            {
                var twoChar = _source.Substring(_pos, 2);
                var twoCharToken = twoChar switch
                {
                    "==" => TokenType.Equal,
                    "!=" => TokenType.NotEqual,
                    "<=" => TokenType.LessEqual,
                    ">=" => TokenType.GreaterEqual,
                    "&&" => TokenType.And,
                    "||" => TokenType.Or,
                    _ => (TokenType?)null
                };
                if (twoCharToken.HasValue)
                {
                    tokens.Add(new Token { Type = twoCharToken.Value, Value = twoChar, Position = _pos });
                    _pos += 2;
                    continue;
                }
            }

            // Tek karakterli tokenlar
            var singleCharToken = ch switch
            {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                '[' => TokenType.LeftBracket,
                ']' => TokenType.RightBracket,
                ';' => TokenType.Semicolon,
                ',' => TokenType.Comma,
                '.' => TokenType.Dot,
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '%' => TokenType.Percent,
                '<' => TokenType.LessThan,
                '>' => TokenType.GreaterThan,
                '!' => TokenType.Not,
                ':' => TokenType.Colon,
                _ => (TokenType?)null
            };

            if (singleCharToken.HasValue)
            {
                tokens.Add(new Token { Type = singleCharToken.Value, Value = ch.ToString(), Position = _pos });
                _pos++;
                continue;
            }

            // Bilinmeyen karakter — atla
            _pos++;
        }

        tokens.Add(new Token { Type = TokenType.EOF, Value = "", Position = _pos });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
            _pos++;
    }

    private Token ReadString(char quote)
    {
        var start = _pos;
        _pos++; // opening quote
        var sb = new System.Text.StringBuilder();

        while (_pos < _source.Length && _source[_pos] != quote)
        {
            if (_source[_pos] == '\\' && _pos + 1 < _source.Length)
            {
                _pos++;
                sb.Append(_source[_pos] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => _source[_pos]
                });
            }
            else
            {
                sb.Append(_source[_pos]);
            }
            _pos++;
        }

        if (_pos < _source.Length) _pos++; // closing quote

        return new Token { Type = TokenType.StringLiteral, Value = sb.ToString(), Position = start };
    }

    private Token ReadNumber()
    {
        var start = _pos;
        var hasDot = false;

        while (_pos < _source.Length && (char.IsDigit(_source[_pos]) || _source[_pos] == '.'))
        {
            if (_source[_pos] == '.')
            {
                if (hasDot) break;
                hasDot = true;
            }
            _pos++;
        }

        return new Token { Type = TokenType.NumberLiteral, Value = _source[start.._pos], Position = start };
    }

    private Token ReadIdentifier()
    {
        var start = _pos;
        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_' || _source[_pos] == '$'))
            _pos++;

        var value = _source[start.._pos];

        // Keyword kontrolü
        return value.ToLowerInvariant() switch
        {
            "true" or "false" => new Token { Type = TokenType.BoolLiteral, Value = value, Position = start },
            "null" or "empty" or "emptystring" => new Token { Type = TokenType.NullLiteral, Value = value, Position = start },
            "and" => new Token { Type = TokenType.And, Value = value, Position = start },
            "or" => new Token { Type = TokenType.Or, Value = value, Position = start },
            "not" => new Token { Type = TokenType.Not, Value = value, Position = start },
            _ => new Token { Type = TokenType.Identifier, Value = value, Position = start }
        };
    }
}
