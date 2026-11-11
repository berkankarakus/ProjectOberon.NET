namespace ProjectOberon.Emulator;

public sealed partial class OberonViewerSystem
{
    public const int DefaultMinimumHeight = 1;

    private readonly List<OberonViewerTrack> _tracks = new();
    private readonly int _minimumHeight;
    private OberonViewer? _lastClosedViewer;
    private OberonViewer? _focusViewer;

    public OberonViewerSystem(
        int width,
        int height,
        int minimumHeight = DefaultMinimumHeight)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (minimumHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHeight));
        }

        Width = width;
        Height = height;
        _minimumHeight = minimumHeight;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<OberonViewerTrack> Tracks => _tracks;

    public OberonViewer? FocusViewer => _focusViewer;

    public OberonViewerTrack AddTrack(int x, int width)
    {
        if (x < 0 || width <= 0 || x > Width - width)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var track = new OberonViewerTrack(x, width, Height);
        _tracks.Add(track);
        _tracks.Sort(static (left, right) => left.X.CompareTo(right.X));
        return track;
    }

    private OberonViewerTrack? FindTrack(int x)
    {
        for (var index = 0; index < _tracks.Count; index++)
        {
            if (_tracks[index].ContainsX(x))
            {
                return _tracks[index];
            }
        }

        return null;
    }

    private OberonViewerTrack? FindTrack(OberonViewer viewer)
    {
        for (var trackIndex = 0; trackIndex < _tracks.Count; trackIndex++)
        {
            if (_tracks[trackIndex].Viewers.Contains(viewer))
            {
                return _tracks[trackIndex];
            }
        }

        return null;
    }

    private bool IsRegistered(OberonViewer viewer)
    {
        return FindTrack(viewer) is not null;
    }

    private static int FindViewerIndex(OberonViewerTrack track, OberonViewer viewer)
    {
        for (var index = 0; index < track.Viewers.Count; index++)
        {
            if (ReferenceEquals(track.Viewers[index], viewer))
            {
                return index;
            }
        }

        return -1;
    }

    private void NotifyModify(OberonViewer viewer)
    {
        viewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Modify)
        {
            Y = viewer.Y,
            Height = viewer.Height
        });
    }
}
