using System.Globalization;
using System.Text;

namespace ProjectOberon.Compiler;

public enum OberonCompilerTokenKind
{
    Invalid = 0,
    Multiply = 1,
    RealDivide = 2,
    Divide = 3,
    Mod = 4,
    And = 5,
    Plus = 6,
    Minus = 7,
    Or = 8,
    Equal = 9,
    NotEqual = 10,
    Less = 11,
    LessOrEqual = 12,
    Greater = 13,
    GreaterOrEqual = 14,
    In = 15,
    Is = 16,
    Arrow = 17,
    Period = 18,
    Character = 20,
    Integer = 21,
    Real = 22,
    False = 23,
    True = 24,
    Nil = 25,
    String = 26,
    Not = 27,
    LeftParenthesis = 28,
    LeftBracket = 29,
    LeftBrace = 30,
    Identifier = 31,
    If = 32,
    While = 34,
    Repeat = 35,
    Case = 36,
    For = 37,
    Comma = 40,
    Colon = 41,
    Becomes = 42,
    Range = 43,
    RightParenthesis = 44,
    RightBracket = 45,
    RightBrace = 46,
    Then = 47,
    Of = 48,
    Do = 49,
    To = 50,
    By = 51,
    Semicolon = 52,
    End = 53,
    Bar = 54,
    Else = 55,
    Elsif = 56,
    Until = 57,
    Return = 58,
    Array = 60,
    Record = 61,
    Pointer = 62,
    Const = 63,
    Type = 64,
    Var = 65,
    Procedure = 66,
    Begin = 67,
    Import = 68,
    Module = 69,
    EndOfText = 70
}

public sealed record OberonCompilerToken(
    OberonCompilerTokenKind Kind,
    string Lexeme,
    int Position,
    int Line,
    int Column,
    int? IntegerValue = null,
    double? RealValue = null,
    int? CharacterValue = null,
    string? StringValue = null);

public sealed record OberonCompilerDiagnostic(
    string Message,
    int Position,
    int Line,
    int Column);

public sealed class OberonCompilerScanner
{
    private readonly string _source;
    private readonly List<OberonCompilerDiagnostic> _diagnostics = [];
    private int _position;
    private int _line;
    private int _column;

    private static readonly IReadOnlyDictionary<string, OberonCompilerTokenKind> Keywords =
        new Dictionary<string, OberonCompilerTokenKind>(StringComparer.Ordinal)
        {
            ["IF"] = OberonCompilerTokenKind.If,
            ["WHILE"] = OberonCompilerTokenKind.While,
            ["REPEAT"] = OberonCompilerTokenKind.Repeat,
            ["CASE"] = OberonCompilerTokenKind.Case,
            ["FOR"] = OberonCompilerTokenKind.For,
            ["FALSE"] = OberonCompilerTokenKind.False,
            ["TRUE"] = OberonCompilerTokenKind.True,
            ["NIL"] = OberonCompilerTokenKind.Nil,
            ["IN"] = OberonCompilerTokenKind.In,
            ["IS"] = OberonCompilerTokenKind.Is,
            ["TO"] = OberonCompilerTokenKind.To,
            ["BY"] = OberonCompilerTokenKind.By,
            ["OR"] = OberonCompilerTokenKind.Or,
            ["DIV"] = OberonCompilerTokenKind.Divide,
            ["MOD"] = OberonCompilerTokenKind.Mod,
            ["ELSE"] = OberonCompilerTokenKind.Else,
            ["THEN"] = OberonCompilerTokenKind.Then,
            ["END"] = OberonCompilerTokenKind.End,
            ["TYPE"] = OberonCompilerTokenKind.Type,
            ["OF"] = OberonCompilerTokenKind.Of,
            ["ELSIF"] = OberonCompilerTokenKind.Elsif,
            ["ARRAY"] = OberonCompilerTokenKind.Array,
            ["BEGIN"] = OberonCompilerTokenKind.Begin,
            ["CONST"] = OberonCompilerTokenKind.Const,
            ["UNTIL"] = OberonCompilerTokenKind.Until,
            ["DO"] = OberonCompilerTokenKind.Do,
            ["RECORD"] = OberonCompilerTokenKind.Record,
            ["RETURN"] = OberonCompilerTokenKind.Return,
            ["IMPORT"] = OberonCompilerTokenKind.Import,
            ["MODULE"] = OberonCompilerTokenKind.Module,
            ["POINTER"] = OberonCompilerTokenKind.Pointer,
            ["PROCEDURE"] = OberonCompilerTokenKind.Procedure,
            ["VAR"] = OberonCompilerTokenKind.Var
        };

    public OberonCompilerScanner(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public IReadOnlyList<OberonCompilerDiagnostic> Diagnostics => _diagnostics;

    public OberonCompilerToken Scan()
    {
        SkipWhitespaceAndComments();
        var start = _position;
        var line = _line;
        var column = _column;
        if (IsEnd)
        {
            return new OberonCompilerToken(OberonCompilerTokenKind.EndOfText, string.Empty,
                start, line, column);
        }

        var current = Peek();
        if (IsLetter(current))
        {
            return ScanIdentifier(start, line, column);
        }
        if (IsDigit(current))
        {
            return ScanNumber(start, line, column);
        }
        if (current == '"')
        {
            return ScanString(start, line, column);
        }
        if (current == '$')
        {
            return ScanHexString(start, line, column);
        }

        Advance();
        return current switch
        {
            '&' => Token(OberonCompilerTokenKind.And, start, line, column),
            '*' => Token(OberonCompilerTokenKind.Multiply, start, line, column),
            '+' => Token(OberonCompilerTokenKind.Plus, start, line, column),
            '-' => Token(OberonCompilerTokenKind.Minus, start, line, column),
            '/' => Token(OberonCompilerTokenKind.RealDivide, start, line, column),
            ',' => Token(OberonCompilerTokenKind.Comma, start, line, column),
            ';' => Token(OberonCompilerTokenKind.Semicolon, start, line, column),
            '(' => Token(OberonCompilerTokenKind.LeftParenthesis, start, line, column),
            ')' => Token(OberonCompilerTokenKind.RightParenthesis, start, line, column),
            '[' => Token(OberonCompilerTokenKind.LeftBracket, start, line, column),
            ']' => Token(OberonCompilerTokenKind.RightBracket, start, line, column),
            '{' => Token(OberonCompilerTokenKind.LeftBrace, start, line, column),
            '}' => Token(OberonCompilerTokenKind.RightBrace, start, line, column),
            '^' => Token(OberonCompilerTokenKind.Arrow, start, line, column),
            '|' => Token(OberonCompilerTokenKind.Bar, start, line, column),
            '~' => Token(OberonCompilerTokenKind.Not, start, line, column),
            '=' => Token(OberonCompilerTokenKind.Equal, start, line, column),
            ':' when Match('=') => Token(OberonCompilerTokenKind.Becomes, start, line, column),
            ':' => Token(OberonCompilerTokenKind.Colon, start, line, column),
            '#' => Token(OberonCompilerTokenKind.NotEqual, start, line, column),
            '<' when Match('=') => Token(OberonCompilerTokenKind.LessOrEqual, start, line, column),
            '<' => Token(OberonCompilerTokenKind.Less, start, line, column),
            '>' when Match('=') => Token(OberonCompilerTokenKind.GreaterOrEqual, start, line, column),
            '>' => Token(OberonCompilerTokenKind.Greater, start, line, column),
            '.' when Match('.') => Token(OberonCompilerTokenKind.Range, start, line, column),
            '.' => Token(OberonCompilerTokenKind.Period, start, line, column),
            _ => Invalid(start, line, column, $"unexpected character '{current}'")
        };
    }

    private OberonCompilerToken ScanIdentifier(int start, int line, int column)
    {
        while (!IsEnd && (IsLetter(Peek()) || IsDigit(Peek()))) Advance();
        var lexeme = _source[start.._position];
        return Keywords.TryGetValue(lexeme, out var keyword)
            ? new OberonCompilerToken(keyword, lexeme, start, line, column)
            : new OberonCompilerToken(OberonCompilerTokenKind.Identifier, lexeme, start, line, column);
    }

    private OberonCompilerToken ScanNumber(int start, int line, int column)
    {
        while (!IsEnd && IsDigit(Peek())) Advance();
        while (!IsEnd && Peek() is >= 'A' and <= 'F') Advance();

        if (!IsEnd && Peek() is 'H' or 'R' or 'X')
        {
            var suffix = Advance();
            var digits = _source[(start)..(_position - 1)];
            if (!uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
                return Invalid(start, line, column, "invalid hexadecimal literal");

            return suffix switch
            {
                'X' when bits <= byte.MaxValue => new OberonCompilerToken(
                    OberonCompilerTokenKind.Character, _source[start.._position], start, line, column,
                    CharacterValue: (int)bits),
                'X' => Invalid(start, line, column, "character literal is outside the byte range"),
                'R' => new OberonCompilerToken(
                    OberonCompilerTokenKind.Real, _source[start.._position], start, line, column,
                    RealValue: BitConverter.Int32BitsToSingle(unchecked((int)bits))),
                _ => new OberonCompilerToken(
                    OberonCompilerTokenKind.Integer, _source[start.._position], start, line, column,
                    IntegerValue: unchecked((int)bits))
            };
        }

        if (!IsEnd && Peek() == '.')
        {
            if (Peek(1) == '.')
            {
                return DecimalInteger(start, line, column);
            }
            Advance();
            while (!IsEnd && IsDigit(Peek())) Advance();
            if (!IsEnd && Peek() is 'E' or 'D')
            {
                Advance();
                if (!IsEnd && Peek() is '+' or '-')
                {
                    Advance();
                }
                if (IsEnd || !IsDigit(Peek()))
                {
                    return Invalid(start, line, column, "digit expected in exponent");
                }
                while (!IsEnd && IsDigit(Peek())) Advance();
            }

            var realLexeme = _source[start.._position];
            return double.TryParse(realLexeme.Replace('D', 'E'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var real)
                ? new OberonCompilerToken(OberonCompilerTokenKind.Real, realLexeme, start, line, column,
                    RealValue: real)
                : Invalid(start, line, column, "invalid real literal");
        }

        var integerLexeme = _source[start.._position];
        return int.TryParse(integerLexeme, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
            ? new OberonCompilerToken(OberonCompilerTokenKind.Integer, integerLexeme, start, line, column,
                IntegerValue: integer)
            : Invalid(start, line, column, "integer literal is too large or invalid");
    }

    private OberonCompilerToken DecimalInteger(int start, int line, int column)
    {
        var lexeme = _source[start.._position];
        return int.TryParse(lexeme, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
            ? new OberonCompilerToken(OberonCompilerTokenKind.Integer, lexeme, start, line, column,
                IntegerValue: integer)
            : Invalid(start, line, column, "invalid integer literal");
    }

    private OberonCompilerToken ScanString(int start, int line, int column)
    {
        Advance();
        var valueStart = _position;
        while (!IsEnd && Peek() != '"' && Peek() >= ' ')
        {
            Advance();
        }

        if (IsEnd || Peek() != '"')
        {
            return Invalid(start, line, column, "unterminated string literal");
        }
        Advance();
        return new OberonCompilerToken(OberonCompilerTokenKind.String, _source[start.._position], start, line, column,
            StringValue: _source[valueStart..(_position - 1)]);
    }

    private OberonCompilerToken ScanHexString(int start, int line, int column)
    {
        Advance();
        var value = new StringBuilder();
        while (!IsEnd && Peek() != '$')
        {
            while (!IsEnd && char.IsWhiteSpace(Peek())) Advance();
            if (IsEnd || Peek() == '$')
            {
                break;
            }
            var high = ReadHexDigit();
            while (!IsEnd && char.IsWhiteSpace(Peek())) Advance();
            var low = ReadHexDigit();
            if (high < 0 || low < 0)
            {
                while (!IsEnd && Peek() != '$') Advance();
                return Invalid(start, line, column, "hexadecimal digit expected");
            }
            value.Append((char)((high << 4) | low));
        }

        if (IsEnd)
        {
            return Invalid(start, line, column, "unterminated hexadecimal string");
        }
        Advance();
        return new OberonCompilerToken(OberonCompilerTokenKind.String, _source[start.._position], start, line, column,
            StringValue: value.ToString());
    }

    private int ReadHexDigit()
    {
        if (IsEnd)
        {
            return -1;
        }
        var value = Peek();
        if (value is >= '0' and <= '9')
        {
            Advance();
            return value - '0';
        }
        if (value is >= 'A' and <= 'F')
        {
            Advance();
            return value - 'A' + 10;
        }
        return -1;
    }

    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            while (!IsEnd && char.IsWhiteSpace(Peek())) Advance();
            if (!IsEnd && Peek() == '(' && Peek(1) == '*')
            {
                SkipComment();
                continue;
            }
            return;
        }
    }

    private void SkipComment()
    {
        var start = _position;
        var line = _line;
        var column = _column;
        var depth = 0;
        while (!IsEnd)
        {
            if (Peek() == '(' && Peek(1) == '*')
            {
                Advance();
                Advance();
                depth++;
            }
            else if (Peek() == '*' && Peek(1) == ')')
            {
                Advance();
                Advance();
                if (--depth == 0)
                {
                    return;
                }
            }
            else Advance();
        }

        _diagnostics.Add(new OberonCompilerDiagnostic("unterminated comment", start, line, column));
    }

    private OberonCompilerToken Token(OberonCompilerTokenKind kind, int start, int line, int column)
    {
        return new OberonCompilerToken(kind, _source[start.._position], start, line, column);
    }

    private OberonCompilerToken Invalid(int start, int line, int column, string message)
    {
        _diagnostics.Add(new OberonCompilerDiagnostic(message, start, line, column));
        return new OberonCompilerToken(OberonCompilerTokenKind.Invalid, _source[start.._position], start, line, column);
    }

    private bool Match(char expected)
    {
        if (IsEnd || Peek() != expected)
        {
            return false;
        }
        Advance();
        return true;
    }

    private char Peek(int offset = 0)
    {
        var position = _position + offset;
        return position < _source.Length ? _source[position] : '\0';
    }

    private char Advance()
    {
        var value = _source[_position++];
        if (value == '\r')
        {
            if (!IsEnd && Peek() == '\n') _position++;
            _line++;
            _column = 0;
        }
        else if (value == '\n')
        {
            _line++;
            _column = 0;
        }
        else _column++;
        return value;
    }

    private bool IsEnd => _position >= _source.Length;

    private static bool IsLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}
