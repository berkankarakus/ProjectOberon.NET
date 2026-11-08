namespace ProjectOberon.Emulator;

public sealed partial class OberonViewerSystem
{
    public bool Open(OberonViewer viewer, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (viewer.IsDisplayed || viewer.IsSuspended)
        {
            return false;
        }

        var track = FindTrack(x);
        if (track is null)
        {
            return false;
        }

        var requestedY = Math.Clamp(y, _minimumHeight, track.Height);
        var insertionIndex = 0;
        var frameY = 0;
        var frameBottom = track.Height;
        OberonViewer? lowerFrame = null;
        for (var index = 0; index < track.Viewers.Count; index++)
        {
            var candidate = track.Viewers[index];
            if (requestedY < candidate.Bottom)
            {
                insertionIndex = index;
                frameY = candidate.Y;
                frameBottom = candidate.Bottom;
                lowerFrame = candidate;
                break;
            }

            insertionIndex = index + 1;
            frameY = candidate.Bottom;
        }

        if (frameBottom - frameY < _minimumHeight * 2)
        {
            return false;
        }

        var splitY = Math.Clamp(requestedY, frameY + _minimumHeight, frameBottom - _minimumHeight);
        if (lowerFrame is not null)
        {
            lowerFrame.Y = splitY;
            lowerFrame.Height = frameBottom - splitY;
            NotifyModify(lowerFrame);
        }

        viewer.SetBounds(track.X, frameY, track.Width, splitY - frameY);
        viewer.State = 2;
        track.AddViewer(viewer, insertionIndex);
        viewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Restore));
        return true;
    }

    public bool Change(OberonViewer viewer, int newBottom)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var track = FindTrack(viewer);
        if (track is null || !viewer.IsDisplayed)
        {
            return false;
        }

        var index = FindViewerIndex(track, viewer);
        var lowerFrame = index + 1 < track.Viewers.Count
            ? track.Viewers[index + 1]
            : null;
        var maximumBottom = lowerFrame?.Bottom ?? track.Height;
        var clampedBottom = Math.Clamp(
            newBottom,
            viewer.Y + _minimumHeight,
            maximumBottom - _minimumHeight);
        var oldBottom = viewer.Bottom;
        if (clampedBottom == oldBottom)
        {
            return false;
        }

        viewer.Height = clampedBottom - viewer.Y;
        if (lowerFrame is not null)
        {
            lowerFrame.Y = clampedBottom;
            lowerFrame.Height = maximumBottom - clampedBottom;
            NotifyModify(lowerFrame);
        }

        NotifyModify(viewer);
        return true;
    }

    public bool Close(OberonViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var track = FindTrack(viewer);
        if (track is null || !viewer.IsDisplayed)
        {
            return false;
        }

        var index = FindViewerIndex(track, viewer);
        var lowerFrame = index + 1 < track.Viewers.Count
            ? track.Viewers[index + 1]
            : null;
        var closedY = viewer.Y;
        var closedHeight = viewer.Height;

        viewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Suspend)
        {
            State = 0
        });
        viewer.State = 0;
        _lastClosedViewer = viewer;
        if (ReferenceEquals(_focusViewer, viewer))
        {
            _focusViewer = null;
        }
        track.RemoveViewer(viewer);

        if (lowerFrame is not null)
        {
            lowerFrame.Y = closedY;
            lowerFrame.Height += closedHeight;
            NotifyModify(lowerFrame);
        }

        return true;
    }

    public OberonViewer? Recall()
    {
        return _lastClosedViewer;
    }

    public int CloseTrack(int x)
    {
        var track = FindTrack(x);
        if (track is null)
        {
            return 0;
        }

        var closedCount = 0;
        foreach (var viewer in track.Viewers.ToArray())
        {
            if (Close(viewer))
            {
                closedCount++;
            }
        }

        return closedCount;
    }
}
