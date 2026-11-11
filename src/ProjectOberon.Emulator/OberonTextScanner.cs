using System.Globalization;

namespace ProjectOberon.Emulator;

public enum OberonScannerTokenKind
{
    Invalid,
    Name,
    String,
    Integer,
    Real,
    Character,
    EndOfText
}

public sealed record OberonScannerToken(
    OberonScannerTokenKind Kind,
    string Lexeme,
    int? IntegerValue,
    double? RealValue,
    char? CharacterValue,
    int Line)
{
    public string? StringValue { get; init; }
}

public sealed class OberonTextScanner
{
    private readonly string _source;
    private int _position;

    internal OberonTextScanner(string source, int position)
    {
        _source = source;
        _position = position;
    }

    public int Line { get; private set; }

    public OberonScannerToken Scan()
    {
        SkipWhitespace();
        var tokenLine = Line;
        if (_position >= _source.Length)
        {
            return new OberonScannerToken(OberonScannerTokenKind.EndOfText, string.Empty, null, null, null, tokenLine);
        }

        var start = _position;
        var current = _source[_position];
        if (IsLetter(current))
        {
            _position++;
            while (_position < _source.Length && IsNameCharacter(_source[_position])) _position++;
            return Token(OberonScannerTokenKind.Name, start, tokenLine);
        }

        if (current == '"')
        {
            _position++;
            var contentStart = _position;
            while (_position < _source.Length && _source[_position] != '"' && _source[_position] >= ' ')
                _position++;
            if (_position >= _source.Length || _source[_position] != '"')
                return Token(OberonScannerTokenKind.Invalid, start, tokenLine);
            var content = _source[contentStart.._position];
            _position++;
            return new OberonScannerToken(OberonScannerTokenKind.String, _source[start.._position], null, null, null, tokenLine)
            {
                StringValue = content
            };
        }

        if ((current == '-' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1])) || char.IsDigit(current))
        {
            return ScanNumber(tokenLine, start);
        }

        _position++;
        return new OberonScannerToken(OberonScannerTokenKind.Character, _source[start.._position], null, null, current, tokenLine);
    }

    private OberonScannerToken ScanNumber(int tokenLine, int start)
    {
        if (_source[_position] == '-') _position++;
        while (_position < _source.Length && char.IsDigit(_source[_position])) _position++;
        while (_position < _source.Length && _source[_position] is >= 'A' and <= 'F') _position++;

        if (_position < _source.Length && _source[_position] == 'H')
        {
            _position++;
            var lexeme = _source[start.._position];
            var negative = lexeme[0] == '-';
            var digits = negative ? lexeme[1..^1] : lexeme[..^1];
            if (!int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                return new OberonScannerToken(OberonScannerTokenKind.Invalid, lexeme, null, null, null, tokenLine);
            return new OberonScannerToken(OberonScannerTokenKind.Integer, lexeme, negative ? -value : value, null, null, tokenLine);
        }

        if (_position < _source.Length && _source[_position] == '.')
        {
            _position++;
            while (_position < _source.Length && char.IsDigit(_source[_position])) _position++;
            if (_position < _source.Length && _source[_position] == 'E')
            {
                _position++;
                if (_position < _source.Length && _source[_position] is '+' or '-') _position++;
                while (_position < _source.Length && char.IsDigit(_source[_position])) _position++;
            }

            var lexeme = _source[start.._position];
            return double.TryParse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? new OberonScannerToken(OberonScannerTokenKind.Real, lexeme, null, value, null, tokenLine)
                : new OberonScannerToken(OberonScannerTokenKind.Invalid, lexeme, null, null, null, tokenLine);
        }

        var integerLexeme = _source[start.._position];
        return int.TryParse(integerLexeme, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? new OberonScannerToken(OberonScannerTokenKind.Integer, integerLexeme, integer, null, null, tokenLine)
            : new OberonScannerToken(OberonScannerTokenKind.Invalid, integerLexeme, null, null, null, tokenLine);
    }

    private OberonScannerToken Token(OberonScannerTokenKind kind, int start, int line)
    {
        return new OberonScannerToken(kind, _source[start.._position], null, null, null, line);
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            if (_source[_position] == '\r') Line++;
            _position++;
        }
    }

    private static bool IsLetter(char value) => char.IsLetter(value) && value <= 0x7f;

    private static bool IsNameCharacter(char value) => IsLetter(value) || char.IsDigit(value) || value == '.';
}
