using System.Buffers.Binary;
using System.Numerics;

namespace ProjectOberon.Emulator;

public sealed class OberonFiles
{
    private readonly OberonFileSystem _fileSystem;

    public OberonFiles(OberonFileSystem fileSystem) => _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public OberonFile? Old(string name)
    {
        ValidateName(name);
        var entry = _fileSystem.Enumerate().FirstOrDefault(item => item.Name == name);
        return entry is null ? null : new OberonFile(entry.Name, _fileSystem.ReadFile(entry), writable: false, _fileSystem.ReadHeader(entry.HeaderAddress).Date);
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length >= 32 || !char.IsAsciiLetter(name[0]) ||
            name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '.')))
            throw new ArgumentException($"invalid Oberon file name: {name}", nameof(name));
    }
}

public sealed class OberonFile
{
    private byte[] _content;
    private readonly bool _writable;

    public OberonFile(string name, ReadOnlySpan<byte> content, bool writable = true, int date = 0)
    {
        OberonFiles.ValidateName(name);
        Name = name;
        Date = date;
        _content = content.ToArray();
        _writable = writable;
    }

    public string Name { get; }
    public int Date { get; }
    public int Length => _content.Length;
    public bool Writable => _writable;

    public OberonRider Set(int position = 0)
    {
        var clamped = Math.Clamp(position, 0, Length);
        return new OberonRider(this, clamped);
    }

    internal byte ReadByte(int position) => _content[position];

    internal void WriteByte(int position, byte value)
    {
        if (!_writable) throw new InvalidOperationException("Oberon file is read-only");
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position));
        if (position >= _content.Length) Array.Resize(ref _content, position + 1);
        _content[position] = value;
    }
}

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
    public OberonFile Base => _file;

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
        for (var index = 0; index < count; index++) destination[index] = _file.ReadByte(Position++);
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
        if (!ReadByte(out var current))
        {
            value = 0;
            return false;
        }
        while (current >= 0x80)
        {
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
        foreach (var value in bytes) WriteByte(value);
    }

    public void Write(char character) => WriteByte((byte)character);

    public void WriteInt(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        Write(bytes);
    }

    public void WriteString(string value)
    {
        foreach (var character in value) Write(character);
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
