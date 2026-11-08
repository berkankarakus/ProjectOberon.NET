namespace ProjectOberon.Emulator;

public sealed partial class OberonTextFrame
{
    public OberonTextFrame(OberonText text, OberonFont font)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Font = font ?? throw new ArgumentNullException(nameof(font));
    }

    public OberonText Text { get; }

    public OberonFont Font { get; }
}
