namespace ProjectOberon.Emulator;

public sealed class OberonFile
{
    private byte[] _content;
    private readonly bool _writable;

    public OberonFile(string name, ReadOnlySpan<byte> content, bool writable = true, int date = 0)
    {
        OberonFileNameRules.Validate(name);
        Name = name;
        Date = date;
        _content = content.ToArray();
        _writable = writable;
    }

    public string Name { get; }
    public int Date { get; }
    public int Length
    {
        get { return _content.Length; }
    }

    public bool Writable
    {
        get { return _writable; }
    }

    public OberonRider Set(int position = 0)
    {
        var clamped = Math.Clamp(position, 0, Length);
        return new OberonRider(this, clamped);
    }

    internal byte ReadByte(int position)
    {
        return _content[position];
    }

    internal void WriteByte(int position, byte value)
    {
        if (!_writable)
        {
            throw new InvalidOperationException("Oberon file is read-only");
        }

        if (position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        if (position >= _content.Length) Array.Resize(ref _content, position + 1);
        _content[position] = value;
    }

    internal ReadOnlySpan<byte> Content
    {
        get { return _content; }
    }

    internal void MarkRegistered(int headerAddress)
    {
        HeaderAddress = headerAddress;
    }

    internal int HeaderAddress { get; private set; }
}
