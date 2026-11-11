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
        var litPixels = UiLogic.UpdateFramebuffer(_surfaceMemory, pixels, flipY);
        if (caret.Y >= 0 && caret.Y + Editor.Frame.Font.Height <= Display.Height)
        {
            DrawModernCaret(pixels, caret.X, caret.Y, Editor.Frame.Font.Height, flipY);
        }

        return litPixels;
    }

    private static void DrawModernCaret(uint[] pixels, int x, int y, int height, bool flipY)
    {
        for (var row = 0; row < height; row++)
        {
            var memoryY = y + row;
            var screenY = flipY ? Display.Height - 1 - memoryY : memoryY;
            if ((uint)screenY >= Display.Height)
            {
                continue;
            }

            for (var column = 0; column < 2; column++)
            {
                var screenX = x + column;
                if ((uint)screenX < Display.Width)
                {
                    pixels[screenY * Display.Width + screenX] = column == 0
                        ? 0xff00_d9ffu
                        : 0xff007a_99u;
                }
            }
        }
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
