namespace ProjectOberon.Emulator;

public enum OberonInputMessageKind
{
    Consume,
    Track
}

public sealed class OberonInputMessage
{
    public OberonInputMessage(OberonInputMessageKind kind)
    {
        Kind = kind;
    }

    public OberonInputMessageKind Kind { get; }

    public uint Keys { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public char Character { get; init; }

    public int KeyCode { get; init; }

    public ushort Modifiers { get; init; }

    public bool ExtendSelection { get; init; }

    public bool Handled { get; set; }
}

public enum OberonControlMessageKind
{
    Defocus,
    Neutralize,
    Mark
}

public sealed class OberonControlMessage
{
    public OberonControlMessage(OberonControlMessageKind kind)
    {
        Kind = kind;
    }

    public OberonControlMessageKind Kind { get; }

    public int X { get; init; }

    public int Y { get; init; }
}
