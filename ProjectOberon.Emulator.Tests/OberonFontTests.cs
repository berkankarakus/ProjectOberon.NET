using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static class OberonFontTests
{
    public static void Run()
    {
        ParsesFontRunsAndGlyphs();
        ParsesExtendedBundledFont();
        RendersGlyphsToDisplayMemory();
        RejectsTruncatedFonts();
    }

    private static void ParsesFontRunsAndGlyphs()
    {
        var font = OberonFont.Parse(BuildFont(), "TestFont");
        var glyph = font.GetGlyph('A');

        TestAssertions.Equal("TestFont", font.Name);
        TestAssertions.Equal(8, font.Height);
        TestAssertions.Equal(3, glyph.Advance);
        TestAssertions.Equal(2, glyph.Width);
        TestAssertions.Equal(2, glyph.Height);
        TestAssertions.Equal((byte)0b0000_0001, glyph.Raster[0]);
        TestAssertions.Equal((byte)0b0000_0010, glyph.Raster[1]);
        TestAssertions.True(font.GetGlyph('B').IsEmpty);
    }

    private static void RendersGlyphsToDisplayMemory()
    {
        var memory = new Memory();
        var display = new OberonDisplay(memory);
        var font = OberonFont.Parse(BuildFont());

        TestAssertions.Equal(3, display.DrawGlyph(font, 'A', 10, 20, Display.White, Display.Replace));
        TestAssertions.True((memory.Read32(Display.Base + (uint)(20 * Display.RowBytes)) & (1u << 10)) != 0);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(21 * Display.RowBytes)) & (1u << 11)) != 0);
        TestAssertions.Equal(6, display.DrawText(font, "AA", 0, 0, Display.White, Display.Replace));
    }

    private static void ParsesExtendedBundledFont()
    {
        foreach (var path in new[] { "apptools/Oberon12b.Scn.Fnt", "apptools/Oberon16.Scn.Fnt" })
        {
            var font = OberonFont.Load(path);

            TestAssertions.True(font.Height > 0);
            TestAssertions.True(font.GetGlyph('\u0080').Advance > 0);
            TestAssertions.True(font.GetGlyph('\u009f').Advance > 0);
        }
    }

    private static void RejectsTruncatedFonts()
    {
        TestAssertions.ThrowsInvalidData(() => OberonFont.Parse([0xdb, 0, 0]));
    }

    private static byte[] BuildFont()
    {
        var data = new List<byte> { 0xdb, 0, 0x53, 0 };
        AddInt16(data, 8);
        AddInt16(data, 0);
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 7);
        AddInt16(data, 1);
        AddInt16(data, 'A');
        AddInt16(data, 'B');
        AddGlyphMetrics(data);
        data.Add(0b0000_0001);
        data.Add(0b0000_0010);
        return data.ToArray();
    }

    private static void AddGlyphMetrics(List<byte> data)
    {
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 0);
        AddInt16(data, 2);
        AddInt16(data, 2);
    }

    private static void AddInt16(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)value));
        data.AddRange(bytes.ToArray());
    }
}
