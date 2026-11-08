namespace ProjectOberon.Emulator;

public sealed partial class OberonMenuViewer
{
    private readonly OberonViewerSystem _system;
    private int _lastViewerHeight;

    public OberonMenuViewer(
        OberonViewerSystem system,
        string name,
        OberonViewerFrame menu,
        OberonViewerFrame main,
        int menuHeight,
        int x,
        int y)
    {
        _system = system ?? throw new ArgumentNullException(nameof(system));
        Menu = menu ?? throw new ArgumentNullException(nameof(menu));
        Main = main ?? throw new ArgumentNullException(nameof(main));
        if (menuHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(menuHeight));
        }

        MenuHeight = menuHeight;
        Viewer = new OberonViewer(name, HandleViewerMessage);
        if (!_system.Open(Viewer, x, y))
        {
            throw new InvalidOperationException("The viewer could not be opened in the requested track.");
        }
    }

    public OberonViewer Viewer { get; }

    public OberonViewerFrame Menu { get; }

    public OberonViewerFrame Main { get; }

    public int MenuHeight { get; }

    public bool Focus()
    {
        return _system.PassFocus(Viewer);
    }

    public bool Resize(int newBottom)
    {
        return _system.Change(Viewer, newBottom);
    }

    public bool DispatchInput(int x, int y, bool resizeRequested = false)
    {
        if (!ReferenceEquals(_system.HitTest(x, y), Viewer))
        {
            return false;
        }

        if (y < Viewer.Y + 1 || y >= Viewer.Bottom)
        {
            return false;
        }

        if (y < Viewer.Bottom - MenuHeight)
        {
            Main.Handle(CreateInputMessage(x, y));
            return true;
        }

        if (resizeRequested)
        {
            return _system.Change(Viewer, y);
        }

        Menu.Handle(CreateInputMessage(x, y));
        return true;
    }
}
