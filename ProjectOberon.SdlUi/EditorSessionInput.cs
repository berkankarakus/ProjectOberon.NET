using ProjectOberon.Emulator;

namespace ProjectOberon.SdlUi;

internal sealed partial class EditorSession
{
    public void InsertText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var character in text.Replace("\n", "\r", StringComparison.Ordinal))
        {
            _eventLoop.ProcessConsumedCharacter(character);
        }
    }

    public bool HandleKey(int key, ushort modifiers)
    {
        return _eventLoop.ProcessConsumedKey(key, modifiers);
    }

    private bool HandleKeyCore(int key, ushort modifiers)
    {
        var extendSelection = (modifiers & SdlNative.KeymodShift) != 0;
        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'a' or 'A')
        {
            Editor.SetCursor(0);
            Editor.SetCursor(Editor.Text.Length, extendSelection: true);
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'c' or 'C')
        {
            _clipboard.SetText(Editor.SelectedText);
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'x' or 'X')
        {
            _clipboard.SetText(Editor.SelectedText);
            Editor.DeleteSelection();
            EnsureCursorVisible();
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'v' or 'V')
        {
            InsertTextCore(_clipboard.GetText());
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'z' or 'Z')
        {
            Editor.Undo();
            EnsureCursorVisible();
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 'y' or 'Y')
        {
            Editor.Redo();
            EnsureCursorVisible();
            return true;
        }

        if ((modifiers & SdlNative.KeymodCtrl) != 0 && key is 's' or 'S')
        {
            Save();
            return true;
        }

        switch (key)
        {
            case 8:
                Editor.Backspace();
                EnsureCursorVisible();
                return true;
            case 127:
                Editor.Delete();
                EnsureCursorVisible();
                return true;
            case 13:
                Editor.Insert("\r");
                EnsureCursorVisible();
                return true;
            case 0x40000050:
                Editor.MoveLeft(extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x4000004f:
                Editor.MoveRight(extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x4000004a:
                Editor.MoveHome(extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x4000004d:
                Editor.MoveEnd(extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x40000052:
                Editor.MoveUp(SurfaceWidth, extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x40000051:
                Editor.MoveDown(SurfaceWidth, extendSelection);
                EnsureCursorVisible();
                return true;
            case 0x4000004b:
                ScrollBy(-1);
                return true;
            case 0x4000004e:
                ScrollBy(1);
                return true;
            default:
                return false;
        }
    }

    public bool HandleMouseButton(bool pressed, byte button, int x, int y, int width, int height, bool flipY)
    {
        if (button != 1)
        {
            return false;
        }

        if (pressed)
        {
            _dragging = true;
            DispatchTrack(x, y, width, height, flipY, extendSelection: false);
        }
        else
        {
            _dragging = false;
        }

        return true;
    }

    public bool HandleMouseMotion(int x, int y, int width, int height, bool flipY, uint state)
    {
        if (!_dragging || (state & 1) == 0)
        {
            return false;
        }

        DispatchTrack(x, y, width, height, flipY, extendSelection: true);
        return true;
    }

    private void HandleInputMessage(OberonViewer viewer, OberonViewerMessage message)
    {
        if (message.Payload is not OberonInputMessage input)
        {
            return;
        }

        if (input.Kind == OberonInputMessageKind.Track)
        {
            SetCursorFromDisplayCoordinates(input.X, input.Y, input.ExtendSelection);
            input.Handled = true;
        }
        else if (input.Character != '\0')
        {
            InsertTextCore(input.Character.ToString());
            input.Handled = true;
        }
        else
        {
            input.Handled = HandleKeyCore(input.KeyCode, input.Modifiers);
        }
    }

    private void InsertTextCore(string text)
    {
        Editor.Insert(text);
        EnsureCursorVisible();
    }

    private void DispatchTrack(int x, int y, int width, int height, bool flipY, bool extendSelection)
    {
        var screenX = InputMapping.ScaleCoordinate(x, width, Display.Width);
        var screenY = InputMapping.ScaleCoordinate(y, height, Display.Height);
        var memoryY = flipY ? Display.Height - 1 - screenY : screenY;
        _flipY = flipY;
        var keys = extendSelection ? 1u : 4u;
        _eventLoop.ProcessMouse(keys, screenX, memoryY, extendSelection);
    }

    private void SetCursorFromDisplayCoordinates(int x, int y, bool extendSelection)
    {
        var lines = Editor.Frame.Layout(SurfaceWidth);
        var originY = GetOriginY(lines.Count, _flipY);
        var position = Editor.Frame.HitTest(
            x,
            y,
            OriginX,
            originY,
            SurfaceWidth,
            ViewportHeight,
            ScrollLine,
            ScrollX);
        Editor.SetCursor(position, extendSelection);
        EnsureCursorVisible();
    }

    private void ScrollBy(int delta)
    {
        _viewport.ScrollBy(delta, SurfaceWidth, ViewportHeight);
    }

    private void EnsureCursorVisible()
    {
        _viewport.EnsureCursorVisible(SurfaceWidth, ViewportHeight);
    }
}
