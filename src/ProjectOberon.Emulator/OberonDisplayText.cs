namespace ProjectOberon.Emulator;

public sealed partial class OberonDisplay
{
    public int DrawGlyph(OberonFont font, char character, int x, int y, int color, int mode)
    {
        ArgumentNullException.ThrowIfNull(font);
        var glyph = font.GetGlyph(character);
        for (var row = 0; row < glyph.Height; row++)
        for (var column = 0; column < glyph.Width; column++)
        {
            var rasterIndex = row * ((glyph.Width + 7) / 8) + column / 8;
            var lit = (glyph.Raster[rasterIndex] & (1 << (column & 7))) != 0;
            if (lit && (uint)(x + glyph.OffsetX + column) < Display.Width &&
                (uint)(y + glyph.OffsetY + row) < Display.Height)
            {
                ApplyPixel(x + glyph.OffsetX + column, y + glyph.OffsetY + row, color, mode);
            }
        }

        return glyph.Advance;
    }

    public int DrawText(OberonFont font, string text, int x, int y, int color, int mode)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);
        foreach (var character in text)
        {
            x += DrawGlyph(font, character, x, y, color, mode);
        }

        return x;
    }
}
