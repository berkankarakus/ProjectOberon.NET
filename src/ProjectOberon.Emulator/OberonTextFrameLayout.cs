namespace ProjectOberon.Emulator;

public sealed partial class OberonTextFrame
{
    public IReadOnlyList<OberonTextLine> Layout(int maximumWidth)
    {
        if (maximumWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }

        var source = Text.GetText();
        if (_layoutCache is not null
            && _layoutWidth == maximumWidth
            && ReferenceEquals(_layoutSource, source))
        {
            return _layoutCache;
        }

        var lines = new List<OberonTextLine>();
        var lineStart = 0;
        var lineWidth = 0;
        var lineLength = 0;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character is '\r' or '\n')
            {
                lines.Add(new OberonTextLine(lineStart, lineLength, lineWidth, source[lineStart..index]));
                if (character == '\r' && index + 1 < source.Length && source[index + 1] == '\n') index++;
                lineStart = index + 1;
                lineWidth = 0;
                lineLength = 0;
                continue;
            }

            var advance = Font.GetGlyph(character).Advance;
            if (WrapLines && lineLength > 0 && lineWidth + advance > maximumWidth)
            {
                lines.Add(new OberonTextLine(lineStart, lineLength, lineWidth, source[lineStart..index]));
                lineStart = index;
                lineWidth = 0;
                lineLength = 0;
            }

            lineWidth += advance;
            lineLength++;
        }

        if (lineLength > 0 || lines.Count == 0 || lineStart < source.Length)
        {
            lines.Add(new OberonTextLine(lineStart, lineLength, lineWidth, source[lineStart..]));
        }

        _layoutSource = source;
        _layoutWidth = maximumWidth;
        _layoutCache = lines;
        return lines;
    }

    public int GetMaxScrollLine(int maximumWidth, int viewportHeight)
    {
        var lines = Layout(maximumWidth);
        var visibleLineCount = GetVisibleLineCount(viewportHeight);
        return Math.Max(0, lines.Count - visibleLineCount);
    }

    public IReadOnlyList<OberonTextLine> VisibleLines(int maximumWidth, int viewportHeight, int scrollLine)
    {
        var lines = Layout(maximumWidth);
        var range = VisibleRange(lines, viewportHeight, scrollLine);
        return lines.Skip(range.Start).Take(range.End - range.Start).ToArray();
    }

    private (int Start, int End) VisibleRange(
        IReadOnlyList<OberonTextLine> lines,
        int viewportHeight,
        int scrollLine)
    {
        if (viewportHeight <= 0)
        {
            return (0, lines.Count);
        }

        var visibleLineCount = GetVisibleLineCount(viewportHeight);
        var start = Math.Clamp(scrollLine, 0, Math.Max(0, lines.Count - visibleLineCount));
        return (start, Math.Min(lines.Count, start + visibleLineCount));
    }

    private int GetVisibleLineCount(int viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return int.MaxValue;
        }

        return Math.Max(1, viewportHeight / Font.Height);
    }
}
