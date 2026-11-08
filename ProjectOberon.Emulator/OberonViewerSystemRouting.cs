namespace ProjectOberon.Emulator;

public sealed partial class OberonViewerSystem
{
    public OberonViewer? HitTest(int x, int y)
    {
        if (y < 0 || y >= Height)
        {
            return null;
        }

        var track = FindTrack(x);
        if (track is null)
        {
            return null;
        }

        for (var index = 0; index < track.Viewers.Count; index++)
        {
            var viewer = track.Viewers[index];
            if (y >= viewer.Y && y < viewer.Bottom)
            {
                return viewer;
            }
        }

        return null;
    }

    public bool DispatchInput(int x, int y, OberonViewerMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var viewer = HitTest(x, y);
        if (viewer is null)
        {
            return false;
        }

        viewer.Handle(message);
        return true;
    }

    public bool DispatchInput(int x, int y, OberonInputMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return DispatchInput(x, y, new OberonViewerMessage(OberonViewerMessageKind.Input)
        {
            X = x,
            Y = y,
            Payload = message
        });
    }

    public bool DispatchControl(int x, int y, OberonControlMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var viewer = HitTest(x, y);
        if (viewer is null)
        {
            return false;
        }

        viewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Control)
        {
            X = x,
            Y = y,
            Payload = message
        });
        return true;
    }

    public bool PassFocus(OberonViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (!viewer.IsDisplayed || !IsRegistered(viewer))
        {
            return false;
        }

        if (ReferenceEquals(_focusViewer, viewer))
        {
            return true;
        }

        if (_focusViewer is not null)
        {
            _focusViewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Control)
            {
                Payload = new OberonControlMessage(OberonControlMessageKind.Defocus)
            });
        }

        _focusViewer = viewer;
        return true;
    }

    public bool DispatchKeyboard(OberonInputMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_focusViewer is null)
        {
            return false;
        }

        _focusViewer.Handle(new OberonViewerMessage(OberonViewerMessageKind.Input)
        {
            Payload = message
        });
        return true;
    }

    public void Broadcast(OberonControlMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Broadcast(new OberonViewerMessage(OberonViewerMessageKind.Control)
        {
            Payload = message
        });
    }

    public void Broadcast(OberonViewerMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        foreach (var track in _tracks)
        {
            foreach (var viewer in track.Viewers.ToArray())
            {
                viewer.Handle(message);
            }
        }
    }
}
