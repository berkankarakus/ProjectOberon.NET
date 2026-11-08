namespace ProjectOberon.Emulator;

public sealed class OberonTextReader
{
    private readonly OberonText _text;

    internal OberonTextReader(OberonText text, int position)
    {
        _text = text;
        Position = position;
    }

    public int Position { get; private set; }
    public bool EndOfText => Position >= _text.Length;
    public OberonTextStyle Style { get; private set; } = OberonTextStyle.Default;

    public bool TryRead(out char character)
    {
        if (!_text.TryRead(Position, out character, out var style))
        {
            return false;
        }

        Style = style;
        Position++;
        return true;
    }
}
