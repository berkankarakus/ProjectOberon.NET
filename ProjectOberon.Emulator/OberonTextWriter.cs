using System.Globalization;

namespace ProjectOberon.Emulator;

public sealed class OberonTextWriter
{
    public OberonTextWriter()
    {
        Buffer = new OberonTextBuffer();
    }

    public OberonTextBuffer Buffer { get; }
    public OberonTextStyle Style { get; private set; } = OberonTextStyle.Default;

    public void SetFont(string? font) => Style = Style with { Font = font };

    public void SetColor(int color) => Style = Style with { Color = color };

    public void SetOffset(int verticalOffset) => Style = Style with { VerticalOffset = verticalOffset };

    public void Write(char character) => Buffer.Append(character, Style);

    public void Write(string text) => Buffer.Append(text, Style);

    public void WriteLine() => Write('\r');

    public void WriteInt(int value, int width = 0)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        Write(formatted.PadLeft(Math.Max(0, width)));
    }

    public void WriteHex(uint value) => Write($" {value:x8}");
}
