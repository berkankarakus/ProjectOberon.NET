using System.Buffers.Binary;

namespace ProjectOberon.Emulator;

public sealed record OberonGlyph(
    int Advance,
    int OffsetX,
    int OffsetY,
    int Width,
    int Height,
    byte[] Raster)
{
    public bool IsEmpty => Width == 0 || Height == 0;
}

public sealed class OberonFont
{
    private const byte FontFileId = 0xdb;
    private readonly OberonGlyph[] _glyphs;

    private OberonFont(
        string name,
        int height,
        int minimumX,
        int maximumX,
        int minimumY,
        int maximumY,
        OberonGlyph[] glyphs)
    {
        Name = name;
        Height = height;
        MinimumX = minimumX;
        MaximumX = maximumX;
        MinimumY = minimumY;
        MaximumY = maximumY;
        _glyphs = glyphs;
    }

    public string Name { get; }
    public int Height { get; }
    public int MinimumX { get; }
    public int MaximumX { get; }
    public int MinimumY { get; }
    public int MaximumY { get; }

    public static OberonFont Load(string path, string? name = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllBytes(path), name ?? System.IO.Path.GetFileName(path));
    }

    public static OberonFont Parse(ReadOnlySpan<byte> data, string name = "font")
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var reader = new FontReader(data);
        if (reader.ReadByte() != FontFileId)
        {
            throw new InvalidDataException("invalid Oberon font file identifier");
        }

        reader.Skip(3); // abstraction, family and variant
        var height = reader.ReadInt16();
        var minimumX = reader.ReadInt16();
        var maximumX = reader.ReadInt16();
        var minimumY = reader.ReadInt16();
        var maximumY = reader.ReadInt16();
        var runCount = reader.ReadInt16();
        if (runCount < 0 || runCount > 128)
        {
            throw new InvalidDataException($"invalid Oberon font run count: {runCount}");
        }

        var runs = new (int Begin, int End)[runCount];
        var glyphCount = 0;
        for (var index = 0; index < runs.Length; index++)
        {
            var begin = reader.ReadInt16();
            var end = reader.ReadInt16();
            if (begin < 0 || end < begin || end > 128)
            {
                throw new InvalidDataException("invalid Oberon font glyph range");
            }

            runs[index] = (begin, end);
            glyphCount = checked(glyphCount + end - begin);
        }

        var glyphs = Enumerable.Range(0, 128)
            .Select(_ => new OberonGlyph(0, 0, 0, 0, 0, []))
            .ToArray();
        for (var index = 0; index < glyphCount; index++)
        {
            var advance = reader.ReadInt16();
            var offsetX = reader.ReadInt16();
            var offsetY = reader.ReadInt16();
            var width = reader.ReadInt16();
            var glyphHeight = reader.ReadInt16();
            if (width < 0 || glyphHeight < 0)
            {
                throw new InvalidDataException("invalid Oberon glyph dimensions");
            }

            var rowBytes = (width + 7) / 8;
            var raster = reader.ReadBytes(checked(rowBytes * glyphHeight));
            glyphs[GlyphIndex(runs, index)] = new OberonGlyph(
                advance, offsetX, offsetY, width, glyphHeight, raster);
        }

        return new OberonFont(name, height, minimumX, maximumX, minimumY, maximumY, glyphs);
    }

    public OberonGlyph GetGlyph(char character) => _glyphs[character & 0x7f];

    private static int GlyphIndex((int Begin, int End)[] runs, int glyphOffset)
    {
        foreach (var (begin, end) in runs)
        {
            var count = end - begin;
            if (glyphOffset < count)
            {
                return begin + glyphOffset;
            }

            glyphOffset -= count;
        }

        throw new InvalidDataException("font glyph data exceeds declared ranges");
    }

    private ref struct FontReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public FontReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _data[_position++];
        }

        public short ReadInt16()
        {
            Ensure(2);
            var value = BinaryPrimitives.ReadInt16LittleEndian(_data[_position..]);
            _position += 2;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0) throw new InvalidDataException("invalid Oberon glyph raster size");
            Ensure(count);
            var result = _data.Slice(_position, count).ToArray();
            _position += count;
            return result;
        }

        public void Skip(int count)
        {
            Ensure(count);
            _position += count;
        }

        private void Ensure(int count)
        {
            if (count < 0 || count > _data.Length - _position)
            {
                throw new InvalidDataException("truncated Oberon font file");
            }
        }
    }
}
