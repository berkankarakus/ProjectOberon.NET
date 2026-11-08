namespace ProjectOberon.Emulator;

public enum OberonFrameMessageKind
{
    Restore,
    Extend,
    Reduce,
    Suspend,
    Input,
    Control
}

public sealed class OberonFrameMessage
{
    public OberonFrameMessage(OberonFrameMessageKind kind)
    {
        Kind = kind;
    }

    public OberonFrameMessageKind Kind { get; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Height { get; init; }

    public int DeltaY { get; init; }

    public object? Payload { get; init; }
}
