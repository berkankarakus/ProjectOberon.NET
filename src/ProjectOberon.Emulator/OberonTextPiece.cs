namespace ProjectOberon.Emulator;

internal sealed class OberonTextPiece
{
    public OberonTextPiece(string text, OberonTextStyle style)
    {
        Text = text;
        Style = style;
    }

    public string Text { get; set; }

    public OberonTextStyle Style { get; set; }
}
