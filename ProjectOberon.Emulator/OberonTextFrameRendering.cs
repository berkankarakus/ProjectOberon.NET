namespace ProjectOberon.Emulator;

public sealed partial class OberonTextFrame
{
    public int Render(
        OberonDisplay display,
        int x,
        int y,
        int maximumWidth,
        int color,
        int mode)
    {
        return Render(display, x, y, maximumWidth, color, mode, viewportHeight: 0, scrollLine: 0);
    }

    public int Render(
        OberonDisplay display,
        int x,
        int y,
        int maximumWidth,
        int color,
        int mode,
        int viewportHeight,
        int scrollLine)
    {
        ArgumentNullException.ThrowIfNull(display);
        var lines = Layout(maximumWidth);
        var range = VisibleRange(lines, viewportHeight, scrollLine);
        for (var index = range.Start; index < range.End; index++)
        {
            display.DrawText(Font, lines[index].Text, x, y + (index - range.Start) * Font.Height, color, mode);
        }

        return y + (range.End - range.Start) * Font.Height;
    }

    public void RenderSelection(
        OberonDisplay display,
        int selectionStart,
        int selectionEnd,
        int originX,
        int originY,
        int maximumWidth)
    {
        RenderSelection(display, selectionStart, selectionEnd, originX, originY, maximumWidth,
            viewportHeight: 0, scrollLine: 0);
    }

    public void RenderSelection(
        OberonDisplay display,
        int selectionStart,
        int selectionEnd,
        int originX,
        int originY,
        int maximumWidth,
        int viewportHeight,
        int scrollLine)
    {
        ArgumentNullException.ThrowIfNull(display);
        ValidateSelection(selectionStart, selectionEnd);
        if (selectionStart == selectionEnd)
        {
            return;
        }

        var lines = Layout(maximumWidth);
        var range = VisibleRange(lines, viewportHeight, scrollLine);
        for (var lineIndex = range.Start; lineIndex < range.End; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineEnd = line.Start + line.Length;
            var begin = Math.Max(selectionStart, line.Start);
            var end = Math.Min(selectionEnd, lineEnd);
            if (begin >= end)
            {
                continue;
            }

            var x = originX + MeasurePrefix(line.Text, begin - line.Start);
            var y = originY + (lineIndex - range.Start) * Font.Height;
            for (var position = begin; position < end; position++)
            {
                var advance = Font.GetGlyph(line.Text[position - line.Start]).Advance;
                display.ReplConst(Display.White, x, y, Math.Max(1, advance), Font.Height, Display.Invert);
                x += advance;
            }
        }
    }

    public void RenderCaret(OberonDisplay display, int position, int originX, int originY, int maximumWidth)
    {
        RenderCaret(display, position, originX, originY, maximumWidth, viewportHeight: 0, scrollLine: 0);
    }

    public void RenderCaret(
        OberonDisplay display,
        int position,
        int originX,
        int originY,
        int maximumWidth,
        int viewportHeight,
        int scrollLine)
    {
        ArgumentNullException.ThrowIfNull(display);
        var caret = GetCaretLocation(position, originX, originY, maximumWidth, viewportHeight, scrollLine);
        if (viewportHeight > 0 &&
            (caret.Y < originY || caret.Y + Font.Height > originY + viewportHeight))
        {
            return;
        }

        display.ReplConst(Display.White, caret.X, caret.Y, 1, Font.Height, Display.Invert);
    }
}
