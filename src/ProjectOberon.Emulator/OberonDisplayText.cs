namespace ProjectOberon.Emulator;

public sealed partial class OberonDisplay
{
    public int DrawGlyph(OberonFont font, char character, int x, int y, int color, int mode)
    {
        ArgumentNullException.ThrowIfNull(font);
        var glyph = font.GetGlyph(character);
        var rowBytes = (glyph.Width + 7) / 8;
        var glyphX = x + glyph.OffsetX;
        var glyphY = y + glyph.OffsetY;
        for (var row = 0; row < glyph.Height; row++)
        {
            var targetY = glyphY + row;
            if ((uint)targetY >= Display.Height)
            {
                continue;
            }

            var left = Math.Max(0, glyphX);
            var right = Math.Min(Display.Width, glyphX + glyph.Width);

            if (glyph.Width <= 32)
            {
                var rasterBits = 0u;
                var rasterOffset = row * rowBytes;
                for (var byteIndex = 0; byteIndex < rowBytes; byteIndex++)
                {
                    rasterBits |= (uint)glyph.Raster[rasterOffset + byteIndex] << (byteIndex * 8);
                }

                for (var wordX = left & ~31; wordX < right; wordX += 32)
                {
                    var start = Math.Max(left, wordX);
                    var end = Math.Min(right, wordX + 32);
                    var count = end - start;
                    var column = start - glyphX;
                    var mask = ((rasterBits >> column) & LowBits(count)) << (start & 31);
                    if (mask != 0)
                    {
                        ApplyWordMask(PixelAddress(wordX, targetY), mask, color, mode);
                    }
                }

                continue;
            }

            for (var wordX = left & ~31; wordX < right; wordX += 32)
            {
                var start = Math.Max(left, wordX);
                var end = Math.Min(right, wordX + 32);
                var mask = 0u;
                for (var targetX = start; targetX < end; targetX++)
                {
                    var column = targetX - glyphX;
                    var rasterIndex = row * rowBytes + column / 8;
                    if ((glyph.Raster[rasterIndex] & (1 << (column & 7))) != 0)
                    {
                        mask |= 1u << (targetX & 31);
                    }
                }

                if (mask != 0)
                {
                    ApplyWordMask(PixelAddress(wordX, targetY), mask, color, mode);
                }
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
