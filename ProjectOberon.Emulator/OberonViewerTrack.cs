namespace ProjectOberon.Emulator;

public sealed class OberonViewerTrack
{
    private readonly List<OberonViewer> _viewers = new();

    internal OberonViewerTrack(int x, int width, int height)
    {
        X = x;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<OberonViewer> Viewers => _viewers;

    public bool ContainsX(int x)
    {
        return x >= X && x < X + Width;
    }

    internal void AddViewer(OberonViewer viewer, int index)
    {
        _viewers.Insert(index, viewer);
    }

    internal void RemoveViewer(OberonViewer viewer)
    {
        _viewers.Remove(viewer);
    }
}
