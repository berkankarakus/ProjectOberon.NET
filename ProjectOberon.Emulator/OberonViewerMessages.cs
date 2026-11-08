namespace ProjectOberon.Emulator;

public enum OberonViewerMessageKind
{
    Restore,
    Modify,
    Suspend,
    Input,
    Control,
    Broadcast
}

public sealed class OberonViewerMessage
{
    public OberonViewerMessage(OberonViewerMessageKind kind)
    {
        Kind = kind;
    }

    public OberonViewerMessageKind Kind { get; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Height { get; init; }

    public int State { get; init; }

    public object? Payload { get; init; }
}
