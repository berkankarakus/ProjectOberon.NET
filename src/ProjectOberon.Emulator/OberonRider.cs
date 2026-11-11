using System.Buffers.Binary;
using System.Numerics;

namespace ProjectOberon.Emulator;

public sealed class OberonRider
{
    private readonly OberonFile _file;

    internal OberonRider(OberonFile file, int position)
    {
        _file = file;
        Position = position;
    }

    public bool Eof { get; private set; }
    public int Result { get; private set; }
    public int Position { get; private set; }
    public OberonFile Base
    {
        get { return _file; }
    }

    public bool ReadByte(out byte value)
    {
        if (Position >= _file.Length)
        {
            value = 0;
            Eof = true;
            return false;
        }

        value = _file.ReadByte(Position++);
        return true;
    }

    public int ReadBytes(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _file.Length - Position);
        for (var index = 0; index < count; index++)
        {
            destination[index] = _file.ReadByte(Position++);
        }
        if (count != destination.Length) Eof = true;
        return count;
    }

    public bool Read(out char character)
    {
        var ok = ReadByte(out var value);
        character = (char)value;
        return ok;
    }

    public bool ReadInt(out int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (ReadBytes(bytes) != bytes.Length)
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        return true;
    }

    public string ReadString()
    {
        var chars = new List<char>();
        while (Read(out var character) && character != '\0') chars.Add(character);
        return new string(chars.ToArray());
    }

    public bool ReadNum(out int value)
    {
        var bits = 32;
        uint packed = 0;
        var continuationCount = 0;
        if (!ReadByte(out var current))
        {
            value = 0;
            return false;
        }
        while (current >= 0x80)
        {
            if (++continuationCount > 4)
            {
                throw new InvalidDataException("Oberon number is too long");
            }

            packed = BitOperations.RotateRight(packed + (uint)(current - 0x80), 7);
            bits -= 7;
            if (!ReadByte(out current))
            {
                value = 0;
                return false;
            }
        }

        value = bits <= 4
            ? (int)BitOperations.RotateRight(packed + (uint)(current & 0x0f), 4)
            : (int)BitOperations.RotateRight(packed + current, 7) >> (bits - 7);
        return true;
    }

    public void WriteByte(byte value)
    {
        _file.WriteByte(Position, value);
        Position++;
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            WriteByte(value);
        }
    }

    public void Write(char character)
    {
        if (character > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(character), "Oberon characters must fit one byte");
        }

        WriteByte((byte)character);
    }

    public void WriteInt(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        Write(bytes);
    }

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0'))
        {
            throw new ArgumentException("Oberon strings cannot contain NUL", nameof(value));
        }

        foreach (var character in value)
        {
            Write(character);
        }
        Write('\0');
    }

    public void WriteNum(int value)
    {
        while (value < -0x40 || value >= 0x40)
        {
            WriteByte((byte)((value & 0x7f) | 0x80));
            value >>= 7;
        }
        WriteByte((byte)(value & 0x7f));
    }
}
