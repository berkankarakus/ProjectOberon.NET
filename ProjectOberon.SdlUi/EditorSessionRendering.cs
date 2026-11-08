using ProjectOberon.Emulator;

namespace ProjectOberon.SdlUi;

internal sealed partial class EditorSession
{
    public int Render(uint[] pixels, bool flipY)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != Display.Width * Display.Height)
        {
            throw new ArgumentException("pixel buffer has the wrong size", nameof(pixels));
        }

        _flipY = flipY;
        var lines = Editor.Frame.Layout(SurfaceWidth);
        var originY = GetOriginY(lines.Count, flipY);
        _surfaceMemory.Clear();
        Editor.Frame.Render(_surfaceDisplay, OriginX, originY, SurfaceWidth, Display.White, Display.Replace,
            ViewportHeight, ScrollLine, ScrollX);
        if (Editor.HasSelection)
        {
            Editor.Frame.RenderSelection(_surfaceDisplay, Editor.SelectionStart, Editor.SelectionEnd,
                OriginX, originY, SurfaceWidth, ViewportHeight, ScrollLine, ScrollX);
        }

        var caret = Editor.Frame.GetCaretLocation(Editor.Cursor, OriginX, originY, SurfaceWidth,
            ViewportHeight, ScrollLine, ScrollX);
        if (caret.Y >= 0 && caret.Y + Editor.Frame.Font.Height <= Display.Height)
        {
            Editor.Frame.RenderCaret(_surfaceDisplay, Editor.Cursor, OriginX, originY, SurfaceWidth,
                ViewportHeight, ScrollLine, ScrollX);
        }

        return UiLogic.UpdateFramebuffer(_surfaceMemory, pixels, flipY);
    }

    private int GetOriginY(int lineCount, bool flipY)
    {
        if (!flipY)
        {
            return OriginY;
        }

        var contentHeight = lineCount * Editor.Frame.Font.Height;
        return Math.Max(0, Display.Height - OriginY - contentHeight);
    }
}
