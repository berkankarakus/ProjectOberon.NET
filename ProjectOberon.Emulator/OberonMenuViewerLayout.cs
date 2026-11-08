namespace ProjectOberon.Emulator;

public sealed partial class OberonMenuViewer
{
    private void LayoutChildren(OberonFrameMessageKind kind)
    {
        var contentWidth = Math.Max(0, Viewer.Width - 2);
        var contentHeight = Math.Max(0, Viewer.Height - 2);
        var menuHeight = Math.Min(MenuHeight, contentHeight);
        var mainHeight = Math.Max(0, contentHeight - menuHeight);
        var mainY = Viewer.Y + 1;
        var menuY = Viewer.Bottom - menuHeight - 1;

        if (mainHeight == 0)
        {
            Menu.SetBounds(Viewer.X + 1, Viewer.Y + 1, contentWidth, contentHeight);
            Main.SetBounds(Viewer.X + 1, Viewer.Bottom - menuHeight, contentWidth, 0);
        }
        else
        {
            Main.SetBounds(Viewer.X + 1, mainY, contentWidth, mainHeight);
            Menu.SetBounds(Viewer.X + 1, menuY, contentWidth, menuHeight);
        }

        var message = new OberonFrameMessage(kind)
        {
            Y = Viewer.Y,
            Height = Viewer.Height
        };
        Menu.Handle(message);
        Main.Handle(message);
        _lastViewerHeight = Viewer.Height;
    }

    private void SuspendChildren()
    {
        Menu.SetBounds(Menu.X, Menu.Y, Menu.Width, 0);
        Main.SetBounds(Main.X, Main.Y, Main.Width, 0);
        var message = new OberonFrameMessage(OberonFrameMessageKind.Suspend)
        {
            Y = Viewer.Y,
            Height = Viewer.Height
        };
        Menu.Handle(message);
        Main.Handle(message);
    }

    private bool IsInsideMain(int x, int y)
    {
        return x >= Main.X &&
            x < Main.X + Main.Width &&
            y >= Main.Y &&
            y < Main.Y + Main.Height;
    }
}
