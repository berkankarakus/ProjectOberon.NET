using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonTextCodec
{
    public const byte TextTag = 0xf1;

    public static byte[] Encode(OberonText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var data = new List<byte> { TextTag };
        AddInt32(data, 0); // fixed up after the run table and text length
        var fonts = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextFont = 1;
        var textLength = 0;

        foreach (var piece in text.Pieces)
        {
            var fontName = piece.Style.Font ?? string.Empty;
            ValidateLatin1(fontName, "font name");
            ValidateLatin1(piece.Text, "text");
            var isNewFont = false;
            if (!fonts.TryGetValue(fontName, out var fontNumber))
            {
                fontNumber = nextFont++;
                if (fontNumber > byte.MaxValue)
                {
                    throw new InvalidDataException("Oberon text contains too many fonts");
                }

                fonts.Add(fontName, fontNumber);
                isNewFont = true;
            }

            if ((uint)piece.Style.Color > byte.MaxValue || piece.Style.VerticalOffset is < sbyte.MinValue or > sbyte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(text), "Oberon text style does not fit the Texts.Mod format");
            }

            data.Add((byte)fontNumber);
            if (isNewFont)
            {
                AddString(data, fontName);
            }
            data.Add((byte)piece.Style.Color);
            data.Add(unchecked((byte)(sbyte)piece.Style.VerticalOffset));
            AddInt32(data, piece.Text.Length);
            textLength = checked(textLength + piece.Text.Length);
        }

        data.Add(0);
        AddInt32(data, textLength);
        var contentOffset = data.Count;

        foreach (var piece in text.Pieces)
        {
            data.AddRange(Encoding.Latin1.GetBytes(piece.Text));
        }

        var result = data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(1, 4), contentOffset);
        return result;
    }

    public static OberonText Decode(ReadOnlySpan<byte> data)
    {
        var reader = new OberonTextBlockReader(data);
        if (reader.ReadByte() != TextTag)
        {
            throw new InvalidDataException("invalid Oberon TextBlock tag");
        }

        var contentOffset = reader.ReadInt32();
        var fonts = new Dictionary<int, string>();
        var runs = new List<(OberonTextStyle Style, int Length)>();
        var totalLength = 0;
        while (true)
        {
            var fontNumber = reader.ReadByte();
            if (fontNumber == 0)
            {
                break;
            }

            if (!fonts.TryGetValue(fontNumber, out var fontName))
            {
                fontName = reader.ReadString();
                fonts.Add(fontNumber, fontName);
            }

            var color = reader.ReadByte();
            var verticalOffset = unchecked((sbyte)reader.ReadByte());
            var length = reader.ReadInt32();
            if (length < 0)
            {
                throw new InvalidDataException("negative Oberon text run length");
            }

            totalLength = checked(totalLength + length);
            runs.Add((new OberonTextStyle(fontName.Length == 0 ? null : fontName, color, verticalOffset), length));
        }

        var declaredLength = reader.ReadInt32();
        if (declaredLength < 0 || declaredLength != totalLength || contentOffset != reader.Position)
        {
            throw new InvalidDataException("invalid Oberon TextBlock length or offset");
        }

        var content = reader.ReadBytes(declaredLength);
        var text = new OberonText();
        var buffer = new OberonTextBuffer();
        var offset = 0;
        foreach (var (style, length) in runs)
        {
            buffer.Append(Encoding.Latin1.GetString(content, offset, length), style);
            offset += length;
        }

        if (reader.Position != data.Length)
        {
            throw new InvalidDataException("unexpected trailing bytes in Oberon TextBlock");
        }

        text.Append(buffer);
        text.MarkClean();
        return text;
    }

    public static OberonText Load(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var data = new byte[file.Length];
        var rider = file.Set();
        if (rider.ReadBytes(data) != data.Length)
        {
            throw new InvalidDataException($"could not read Oberon text {file.Name}");
        }

        return Decode(data);
    }

    public static OberonFile CreateFile(string name, OberonText text, int date = 0)
    {
        return new OberonFile(name, Encode(text), writable: true, date);
    }

    private static void AddInt32(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        data.AddRange(bytes.ToArray());
    }

    private static void AddString(List<byte> data, string value)
    {
        data.AddRange(Encoding.Latin1.GetBytes(value));
        data.Add(0);
    }

    private static void ValidateLatin1(string value, string description)
    {
        if (value.Contains('\0') || value.Any(character => character > byte.MaxValue))
        {
            throw new InvalidDataException($"Oberon {description} must fit Latin-1 and cannot contain NUL");
        }
    }

}
