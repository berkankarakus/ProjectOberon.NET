namespace ProjectOberon.Emulator;

public sealed class OberonViewerFrame
{
    private readonly Action<OberonViewerFrame, OberonFrameMessage>? _handler;

    public OberonViewerFrame(
        string name,
        Action<OberonViewerFrame, OberonFrameMessage>? handler = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A frame name is required.", nameof(name))
            : name;
        _handler = handler;
    }

    public string Name { get; }

    public int X { get; private set; }

    public int Y { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Handle(OberonFrameMessage message)
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
