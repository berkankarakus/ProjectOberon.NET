namespace ProjectOberon.Emulator;

public sealed class OberonTextFrameViewer
{
    private readonly OberonTextViewportController _viewport;
    private bool _initialized;

    public OberonTextFrameViewer(
        OberonViewerSystem system,
        string name,
        OberonTextFrame frame,
        int menuHeight,
        int x,
        int y)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(frame);

        Frame = frame;
        Editor = new OberonTextEditor(frame);
        _viewport = new OberonTextViewportController(Editor);
        var menu = new OberonViewerFrame($"{name}.menu");
        var main = new OberonViewerFrame($"{name}.main", HandleMainFrameMessage);
        Viewer = new OberonMenuViewer(system, name, menu, main, menuHeight, x, y);
        _initialized = true;
        EnsureCursorVisible();
    }

    public OberonTextFrame Frame { get; }

    public OberonTextEditor Editor { get; }

    public OberonMenuViewer Viewer { get; }

    public int ScrollLine => _viewport.ScrollLine;

    public bool Focus()
    {
        return Viewer.Focus();
    }

    public void SetCursor(int position, bool extendSelection = false)
    {
        Editor.SetCursor(position, extendSelection);
        EnsureCursorVisible();
    }

    public bool SetCursorFromPoint(int x, int y, bool extendSelection = false)
    {
        if (!IsInsideMainFrame(x, y))
        {
            return false;
        }

        var position = Frame.HitTest(
            x,
            y,
            Viewer.Main.X,
            Viewer.Main.Y,
            Viewer.Main.Width,
            Viewer.Main.Height,
            ScrollLine);
        Editor.SetCursor(position, extendSelection);
        EnsureCursorVisible();
        return true;
    }

    public void InsertText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Editor.Insert(value);
        EnsureCursorVisible();
    }

    public bool Resize(int newBottom)
    {
        var changed = Viewer.Resize(newBottom);
        EnsureCursorVisible();
        return changed;
    }

    public void ScrollBy(int delta)
    {
        _viewport.ScrollBy(delta, Viewer.Main.Width, Viewer.Main.Height);
    }

    public int Render(
        OberonDisplay display,
        int color = Display.White,
        int mode = Display.Replace)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (Viewer.Main.Width <= 0 || Viewer.Main.Height <= 0)
        {
            return Viewer.Main.Y;
        }

        _viewport.ClampScrollLine(Viewer.Main.Width, Viewer.Main.Height);
        Frame.Render(
            display,
            Viewer.Main.X,
            Viewer.Main.Y,
            Viewer.Main.Width,
            color,
            mode,
            Viewer.Main.Height,
            ScrollLine);

        if (Editor.HasSelection)
        {
            Frame.RenderSelection(
                display,
                Editor.SelectionStart,
                Editor.SelectionEnd,
                Viewer.Main.X,
                Viewer.Main.Y,
                Viewer.Main.Width,
                Viewer.Main.Height,
                ScrollLine);
        }

        var caret = Frame.GetCaretLocation(
            Editor.Cursor,
            Viewer.Main.X,
            Viewer.Main.Y,
            Viewer.Main.Width,
            Viewer.Main.Height,
            ScrollLine);
        if (caret.Y >= Viewer.Main.Y && caret.Y + Frame.Font.Height <= Viewer.Main.Y + Viewer.Main.Height)
        {
            Frame.RenderCaret(
                display,
                Editor.Cursor,
                Viewer.Main.X,
                Viewer.Main.Y,
                Viewer.Main.Width,
                Viewer.Main.Height,
                ScrollLine);
        }

        return Viewer.Main.Y + Viewer.Main.Height;
    }

    private void HandleMainFrameMessage(OberonViewerFrame frame, OberonFrameMessage message)
    {
        if (!_initialized)
        {
            return;
        }

        if (message.Kind == OberonFrameMessageKind.Input)
        {
            if (message.Payload is OberonInputMessage input)
            {
                HandleInput(input);
            }
            else
            {
                SetCursorFromPoint(message.X, message.Y);
            }
        }

        EnsureCursorVisible();
    }

    private void HandleInput(OberonInputMessage input)
    {
        if (input.Kind == OberonInputMessageKind.Track)
        {
            SetCursorFromPoint(input.X, input.Y);
        }
        else if (input.Character != '\0')
        {
            InsertText(input.Character.ToString());
        }
        else
        {
            input.Handled = HandleKey(input);
        }
    }

    private bool HandleKey(OberonInputMessage input)
    {
        var extendSelection = (input.Modifiers & 0x0003) != 0;
        switch (input.KeyCode)
        {
            case OberonKeyCodes.Backspace:
                Editor.Backspace();
                return true;
            case OberonKeyCodes.Delete:
                Editor.Delete();
                return true;
            case OberonKeyCodes.Enter:
                Editor.Insert("\r");
                return true;
            case OberonKeyCodes.Left:
                Editor.MoveLeft(extendSelection);
                return true;
            case OberonKeyCodes.Right:
                Editor.MoveRight(extendSelection);
                return true;
            case OberonKeyCodes.Up:
                Editor.MoveUp(Viewer.Main.Width, extendSelection);
                return true;
            case OberonKeyCodes.Down:
                Editor.MoveDown(Viewer.Main.Width, extendSelection);
                return true;
            case OberonKeyCodes.Home:
                Editor.MoveHome(extendSelection);
                return true;
            case OberonKeyCodes.End:
                Editor.MoveEnd(extendSelection);
                return true;
            default:
                return false;
        }
    }

    private bool IsInsideMainFrame(int x, int y)
    {
        return x >= Viewer.Main.X &&
            x < Viewer.Main.X + Viewer.Main.Width &&
            y >= Viewer.Main.Y &&
            y < Viewer.Main.Y + Viewer.Main.Height;
    }

    private int GetMaximumScrollLine()
    {
        return _viewport.GetMaximumScrollLine(Viewer.Main.Width, Viewer.Main.Height);
    }

    private void EnsureCursorVisible()
    {
        _viewport.EnsureCursorVisible(Viewer.Main.Width, Viewer.Main.Height);
    }
}
