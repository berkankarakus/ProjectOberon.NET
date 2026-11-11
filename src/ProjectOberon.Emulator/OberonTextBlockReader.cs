using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

internal ref struct OberonTextBlockReader
{
    private readonly ReadOnlySpan<byte> _data;

    public OberonTextBlockReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        Position = 0;
    }

    public int Position { get; private set; }

    public byte ReadByte()
    {
        Ensure(1);
        return _data[Position++];
    }

    public int ReadInt32()
    {
        Ensure(4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data[Position..]);
        Position += 4;
        return value;
    }

    public byte[] ReadBytes(int count)
    {
        Ensure(count);
        var result = _data.Slice(Position, count).ToArray();
        Position += count;
        return result;
    }

    public string ReadString()
    {
        var start = Position;
        while (Position < _data.Length && _data[Position] != 0)
        {
            Position++;
        }
        if (Position >= _data.Length)
        {
            throw new InvalidDataException("unterminated Oberon font name");
        }
        var result = Encoding.Latin1.GetString(_data[start..Position]);
        Position++;
        return result;
    }

    private void Ensure(int count)
    {
        if (count < 0 || count > _data.Length - Position)
        {
            throw new InvalidDataException("truncated Oberon TextBlock");
        }
    }
}
