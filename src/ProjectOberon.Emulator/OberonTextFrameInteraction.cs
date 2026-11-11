namespace ProjectOberon.Emulator;

public sealed partial class OberonTextFrame
{
    public OberonCaretLocation GetCaretLocation(int position, int originX, int originY, int maximumWidth)
    {
        return GetCaretLocation(position, originX, originY, maximumWidth, viewportHeight: 0, scrollLine: 0, horizontalScroll: 0);
    }

    public OberonCaretLocation GetCaretLocation(
        int position,
        int originX,
        int originY,
        int maximumWidth,
        int viewportHeight,
        int scrollLine,
        int horizontalScroll = 0)
    {
        if (position < 0 || position > Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var lines = Layout(maximumWidth);
        var range = VisibleRange(lines, viewportHeight, scrollLine);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (position <= line.Start + line.Length || index == lines.Count - 1)
            {
                var width = MeasurePrefix(line.Text, position - line.Start);
                return new OberonCaretLocation(originX + width - horizontalScroll,
                    originY + (index - range.Start) * Font.Height, index);
            }
        }

        return new OberonCaretLocation(originX, originY, 0);
    }

    public OberonCaretLocation GetCaretLocation(
        int position,
        int originX,
        int originY,
        int maximumWidth,
        int scrollLine)
    {
        return GetCaretLocation(position, originX, originY, maximumWidth, viewportHeight: 0, scrollLine, horizontalScroll: 0);
    }

    public int HitTest(int x, int y, int originX, int originY, int maximumWidth)
    {
        return HitTest(x, y, originX, originY, maximumWidth, viewportHeight: 0, scrollLine: 0, horizontalScroll: 0);
    }

    public int HitTest(
        int x,
        int y,
        int originX,
        int originY,
        int maximumWidth,
        int viewportHeight,
        int scrollLine,
        int horizontalScroll = 0)
    {
        var lines = Layout(maximumWidth);
        var range = VisibleRange(lines, viewportHeight, scrollLine);
        var visibleLine = (y - originY) / Font.Height;
        visibleLine = Math.Clamp(visibleLine, 0, range.End - range.Start - 1);
        var line = lines[range.Start + visibleLine];
        var contentX = x + horizontalScroll;
        if (contentX <= originX)
        {
            return line.Start;
        }

        var cursorX = originX;
        for (var index = 0; index < line.Text.Length; index++)
        {
            var advance = Font.GetGlyph(line.Text[index]).Advance;
            if (contentX < cursorX + (advance + 1) / 2)
            {
                return line.Start + index;
            }

            cursorX += advance;
        }

        return line.Start + line.Length;
    }

    private void ValidateSelection(int selectionStart, int selectionEnd)
    {
        if (selectionStart < 0 || selectionEnd < selectionStart || selectionEnd > Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionEnd));
        }
    }

    private int MeasurePrefix(string line, int length)
    {
        var width = 0;
        for (var index = 0; index < Math.Clamp(length, 0, line.Length); index++)
        {
            width += Font.GetGlyph(line[index]).Advance;
        }

        return width;
    }
}
