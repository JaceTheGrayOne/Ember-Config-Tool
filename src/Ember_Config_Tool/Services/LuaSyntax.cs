using System.Globalization;
using System.Text;

namespace Ember_Config_Tool.Services;

public readonly record struct SourceLocation(string FilePath, int Line, int Column)
{
    public override string ToString() => $"{FilePath}:{Line}:{Column}";
}

public enum LuaValueKind
{
    Nil,
    Boolean,
    Number,
    String,
    Table
}

public abstract record LuaValue(LuaValueKind Kind)
{
    public virtual string ToCanonicalString() => Kind.ToString();
}

public sealed record LuaNilValue() : LuaValue(LuaValueKind.Nil)
{
    public static LuaNilValue Instance { get; } = new();
    public override string ToCanonicalString() => "nil";
}

public sealed record LuaBooleanValue(bool Value) : LuaValue(LuaValueKind.Boolean)
{
    public override string ToCanonicalString() => Value ? "true" : "false";
}

public sealed record LuaNumberValue(decimal Value, string RawText) : LuaValue(LuaValueKind.Number)
{
    public bool IsInteger => decimal.Truncate(Value) == Value && !RawText.Contains('.', StringComparison.Ordinal);

    public override string ToCanonicalString()
    {
        return Value.ToString("0.#############################", CultureInfo.InvariantCulture);
    }
}

public sealed record LuaStringValue(string Value) : LuaValue(LuaValueKind.String)
{
    public override string ToCanonicalString() => Value;
}

public sealed record LuaTableValue(IReadOnlyList<LuaTableEntry> Entries) : LuaValue(LuaValueKind.Table)
{
    public IEnumerable<LuaTableEntry> ArrayEntries => Entries.Where(entry => entry.Key is null && entry.IdentifierKey is null);

    public IEnumerable<LuaTableEntry> KeyedEntries => Entries.Where(entry => entry.Key is not null || entry.IdentifierKey is not null);

    public bool TryGetField(string key, out LuaValue value)
    {
        if (TryGetFieldEntry(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = LuaNilValue.Instance;
        return false;
    }

    public bool TryGetFieldEntry(string key, out LuaTableEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate.IdentifierKey is not null && candidate.IdentifierKey.Equals(key, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }

            if (candidate.Key is LuaStringValue stringKey && stringKey.Value.Equals(key, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        entry = new LuaTableEntry(null, null, LuaNilValue.Instance, new SourceLocation("", 0, 0));
        return false;
    }

    public override string ToCanonicalString()
    {
        var parts = Entries.Select(entry =>
        {
            var key = entry.IdentifierKey ?? entry.Key?.ToCanonicalString() ?? "";
            return $"{key}:{entry.Value.ToCanonicalString()}";
        });
        return "{" + string.Join(",", parts) + "}";
    }
}

public sealed record LuaTableEntry(string? IdentifierKey, LuaValue? Key, LuaValue Value, SourceLocation Location);

public sealed record LuaAssignment(IReadOnlyList<string> Path, LuaValue Value, SourceLocation Location)
{
    public string PathText => string.Join(".", Path);
}

public sealed class LuaParseException : Exception
{
    public LuaParseException(string message, SourceLocation location)
        : base($"{message} at {location}")
    {
        Location = location;
    }

    public SourceLocation Location { get; }
}

public static class LuaLiteralParser
{
    public static IReadOnlyList<LuaAssignment> ParseAssignments(string text, string filePath)
    {
        var parser = new Parser(text, filePath);
        return parser.ParseAssignments();
    }

    private enum TokenKind
    {
        Identifier,
        Number,
        String,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Equals,
        Comma,
        Dot,
        Eof
    }

    private readonly record struct Token(TokenKind Kind, string Text, SourceLocation Location);

    private sealed class Parser
    {
        private readonly Lexer _lexer;
        private Token _current;
        private Token _next;

        public Parser(string text, string filePath)
        {
            _lexer = new Lexer(text, filePath);
            _current = _lexer.Next();
            _next = _lexer.Next();
        }

        public IReadOnlyList<LuaAssignment> ParseAssignments()
        {
            var assignments = new List<LuaAssignment>();
            while (_current.Kind != TokenKind.Eof)
            {
                var location = _current.Location;
                var path = ParsePath();
                Expect(TokenKind.Equals);
                var value = ParseValue();
                assignments.Add(new LuaAssignment(path, value, location));
                Accept(TokenKind.Comma);
            }

            return assignments;
        }

        private IReadOnlyList<string> ParsePath()
        {
            var path = new List<string> { Expect(TokenKind.Identifier).Text };

            while (_current.Kind is TokenKind.Dot or TokenKind.LeftBracket)
            {
                if (Accept(TokenKind.Dot))
                {
                    path.Add(Expect(TokenKind.Identifier).Text);
                    continue;
                }

                Expect(TokenKind.LeftBracket);
                if (_current.Kind == TokenKind.String || _current.Kind == TokenKind.Number || _current.Kind == TokenKind.Identifier)
                {
                    path.Add(_current.Text);
                    Advance();
                }
                else
                {
                    throw Error("Expected a string, number, or identifier path segment");
                }

                Expect(TokenKind.RightBracket);
            }

            return path;
        }

        private LuaValue ParseValue()
        {
            return _current.Kind switch
            {
                TokenKind.Identifier => ParseIdentifierValue(),
                TokenKind.Number => ParseNumberValue(),
                TokenKind.String => ParseStringValue(),
                TokenKind.LeftBrace => ParseTableValue(),
                _ => throw Error("Expected a Lua literal value")
            };
        }

        private LuaValue ParseIdentifierValue()
        {
            var token = Expect(TokenKind.Identifier);
            return token.Text switch
            {
                "nil" => LuaNilValue.Instance,
                "true" => new LuaBooleanValue(true),
                "false" => new LuaBooleanValue(false),
                _ => throw new LuaParseException($"Unsupported identifier literal '{token.Text}'", token.Location)
            };
        }

        private LuaValue ParseNumberValue()
        {
            var token = Expect(TokenKind.Number);
            if (!decimal.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new LuaParseException($"Invalid number '{token.Text}'", token.Location);
            }

            return new LuaNumberValue(value, token.Text);
        }

        private LuaValue ParseStringValue()
        {
            var token = Expect(TokenKind.String);
            return new LuaStringValue(token.Text);
        }

        private LuaValue ParseTableValue()
        {
            var entries = new List<LuaTableEntry>();
            Expect(TokenKind.LeftBrace);

            while (_current.Kind != TokenKind.RightBrace)
            {
                if (_current.Kind == TokenKind.Eof)
                {
                    throw Error("Unclosed table literal");
                }

                var location = _current.Location;
                if (_current.Kind == TokenKind.Identifier && _next.Kind == TokenKind.Equals)
                {
                    var key = Expect(TokenKind.Identifier).Text;
                    Expect(TokenKind.Equals);
                    entries.Add(new LuaTableEntry(key, null, ParseValue(), location));
                }
                else if (_current.Kind == TokenKind.LeftBracket)
                {
                    Advance();
                    var key = ParseValue();
                    Expect(TokenKind.RightBracket);
                    Expect(TokenKind.Equals);
                    entries.Add(new LuaTableEntry(null, key, ParseValue(), location));
                }
                else
                {
                    entries.Add(new LuaTableEntry(null, null, ParseValue(), location));
                }

                if (!Accept(TokenKind.Comma) && _current.Kind != TokenKind.RightBrace)
                {
                    throw Error("Expected ',' or '}' in table literal");
                }
            }

            Expect(TokenKind.RightBrace);
            return new LuaTableValue(entries);
        }

        private bool Accept(TokenKind kind)
        {
            if (_current.Kind != kind)
            {
                return false;
            }

            Advance();
            return true;
        }

        private Token Expect(TokenKind kind)
        {
            if (_current.Kind != kind)
            {
                throw Error($"Expected {kind}");
            }

            var token = _current;
            Advance();
            return token;
        }

        private void Advance()
        {
            _current = _next;
            _next = _lexer.Next();
        }

        private LuaParseException Error(string message)
        {
            return new LuaParseException(message, _current.Location);
        }
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private readonly string _filePath;
        private int _index;
        private int _line = 1;
        private int _column = 1;

        public Lexer(string text, string filePath)
        {
            _text = text;
            _filePath = filePath;
        }

        public Token Next()
        {
            SkipTrivia();
            var location = Location();
            if (IsAtEnd)
            {
                return new Token(TokenKind.Eof, "", location);
            }

            var ch = Peek();
            return ch switch
            {
                '{' => Single(TokenKind.LeftBrace),
                '}' => Single(TokenKind.RightBrace),
                '[' => Single(TokenKind.LeftBracket),
                ']' => Single(TokenKind.RightBracket),
                '=' => Single(TokenKind.Equals),
                ',' or ';' => Single(TokenKind.Comma),
                '.' => Single(TokenKind.Dot),
                '"' or '\'' => ReadString(),
                _ when IsIdentifierStart(ch) => ReadIdentifier(),
                _ when IsNumberStart(ch) => ReadNumber(),
                _ => throw new LuaParseException($"Unexpected character '{ch}'", location)
            };
        }

        private Token Single(TokenKind kind)
        {
            var location = Location();
            var ch = Peek();
            Advance();
            return new Token(kind, ch.ToString(), location);
        }

        private Token ReadIdentifier()
        {
            var location = Location();
            var start = _index;
            Advance();
            while (!IsAtEnd && IsIdentifierPart(Peek()))
            {
                Advance();
            }

            return new Token(TokenKind.Identifier, _text[start.._index], location);
        }

        private Token ReadNumber()
        {
            var location = Location();
            var start = _index;
            if (Peek() is '+' or '-')
            {
                Advance();
            }

            while (!IsAtEnd && char.IsDigit(Peek()))
            {
                Advance();
            }

            if (!IsAtEnd && Peek() == '.')
            {
                Advance();
                while (!IsAtEnd && char.IsDigit(Peek()))
                {
                    Advance();
                }
            }

            if (!IsAtEnd && Peek() is 'e' or 'E')
            {
                Advance();
                if (!IsAtEnd && Peek() is '+' or '-')
                {
                    Advance();
                }

                while (!IsAtEnd && char.IsDigit(Peek()))
                {
                    Advance();
                }
            }

            return new Token(TokenKind.Number, _text[start.._index], location);
        }

        private Token ReadString()
        {
            var location = Location();
            var quote = Peek();
            Advance();
            var builder = new StringBuilder();

            while (!IsAtEnd && Peek() != quote)
            {
                var ch = Peek();
                Advance();
                if (ch == '\\')
                {
                    if (IsAtEnd)
                    {
                        throw new LuaParseException("Unclosed escape sequence", location);
                    }

                    var escaped = Peek();
                    Advance();
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        '\'' => '\'',
                        _ => escaped
                    });
                }
                else
                {
                    builder.Append(ch);
                }
            }

            if (IsAtEnd)
            {
                throw new LuaParseException("Unclosed string literal", location);
            }

            Advance();
            return new Token(TokenKind.String, builder.ToString(), location);
        }

        private void SkipTrivia()
        {
            while (!IsAtEnd)
            {
                if (char.IsWhiteSpace(Peek()))
                {
                    Advance();
                    continue;
                }

                if (Peek() == '-' && Peek(1) == '-')
                {
                    SkipComment();
                    continue;
                }

                break;
            }
        }

        private void SkipComment()
        {
            Advance();
            Advance();
            if (!IsAtEnd && Peek() == '[' && Peek(1) == '[')
            {
                Advance();
                Advance();
                while (!IsAtEnd)
                {
                    if (Peek() == ']' && Peek(1) == ']')
                    {
                        Advance();
                        Advance();
                        return;
                    }

                    Advance();
                }

                return;
            }

            while (!IsAtEnd && Peek() != '\n')
            {
                Advance();
            }
        }

        private bool IsAtEnd => _index >= _text.Length;

        private char Peek(int offset = 0)
        {
            var position = _index + offset;
            return position >= _text.Length ? '\0' : _text[position];
        }

        private void Advance()
        {
            if (IsAtEnd)
            {
                return;
            }

            if (_text[_index] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            _index++;
        }

        private SourceLocation Location() => new(_filePath, _line, _column);

        private static bool IsIdentifierStart(char ch)
        {
            return char.IsAsciiLetter(ch) || ch == '_';
        }

        private static bool IsIdentifierPart(char ch)
        {
            return char.IsAsciiLetterOrDigit(ch) || ch == '_';
        }

        private bool IsNumberStart(char ch)
        {
            if (char.IsDigit(ch))
            {
                return true;
            }

            return (ch == '-' || ch == '+') && char.IsDigit(Peek(1));
        }
    }
}
