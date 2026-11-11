namespace ProjectOberon.Emulator;

public sealed class OberonViewer
{
    private readonly Action<OberonViewer, OberonViewerMessage>? _handler;

    public OberonViewer(
        string name,
        Action<OberonViewer, OberonViewerMessage>? handler = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A viewer name is required.", nameof(name))
            : name;
        _handler = handler;
    }

    public string Name { get; }

    public int X { get; internal set; }

    public int Y { get; internal set; }

    public int Width { get; internal set; }

    public int Height { get; internal set; }

    // 0 = closed, 1 = filler, >1 = displayed, <0 = suspended.
    public int State { get; internal set; }

    public bool IsDisplayed => State > 1;

    public bool IsSuspended => State < 0;

    public int Bottom => Y + Height;

    public void Handle(OberonViewerMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _handler?.Invoke(this, message);
    }

    internal void SetBounds(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
