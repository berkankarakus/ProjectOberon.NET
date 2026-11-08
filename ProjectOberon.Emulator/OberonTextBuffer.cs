namespace ProjectOberon.Emulator;

public sealed class OberonTextBuffer
{
    private readonly List<OberonTextPiece> _pieces = [];

    public int Length { get; private set; }

    public void Append(string text, OberonTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return;
        }

        if (_pieces.Count > 0 && _pieces[^1].Style == style)
        {
            _pieces[^1].Text += text;
        }
        else
        {
            _pieces.Add(new OberonTextPiece(text, style));
        }

        Length += text.Length;
    }

    public void Append(string text) => Append(text, OberonTextStyle.Default);

    public void Append(char character, OberonTextStyle style) => Append(character.ToString(), style);

    public void CopyFrom(OberonTextBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var piece in source._pieces)
        {
            Append(piece.Text, piece.Style);
        }
    }

    internal IReadOnlyList<OberonTextPiece> Pieces => _pieces;

    internal void Clear()
    {
        _pieces.Clear();
        Length = 0;
    }
}
