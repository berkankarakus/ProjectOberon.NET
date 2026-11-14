namespace ProjectOberon.Emulator;

public sealed class OberonTextViewportController
{
    public OberonTextViewportController(OberonTextEditor editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public OberonTextEditor Editor { get; }

    public int ScrollLine { get; private set; }

    public int ScrollX { get; private set; }

    public int GetMaximumScrollLine(int maximumWidth, int viewportHeight)
    {
        if (maximumWidth <= 0 || viewportHeight <= 0)
        {
            return 0;
        }

        return Editor.Frame.GetMaxScrollLine(maximumWidth, viewportHeight);
    }

    public void ScrollBy(int delta, int maximumWidth, int viewportHeight)
    {
        var maximum = GetMaximumScrollLine(maximumWidth, viewportHeight);
        ScrollLine = Math.Clamp(ScrollLine + delta, 0, maximum);
    }

    public void ClampScrollLine(int maximumWidth, int viewportHeight)
    {
        ScrollLine = Math.Clamp(
            ScrollLine,
            0,
            GetMaximumScrollLine(maximumWidth, viewportHeight));
    }

    public void EnsureCursorVisible(int maximumWidth, int viewportHeight)
    {
        var maximum = GetMaximumScrollLine(maximumWidth, viewportHeight);
        if (maximum == 0)
        {
            ScrollLine = 0;
        }
        else
        {
            var caret = Editor.Frame.GetCaretLocation(
                Editor.Cursor,
                originX: 0,
                originY: 0,
                maximumWidth: maximumWidth,
                viewportHeight: viewportHeight,
                scrollLine: ScrollLine);
            var visibleLineCount = Math.Max(1, viewportHeight / Editor.Frame.Font.Height);
            if (caret.Line < ScrollLine)
            {
                ScrollLine = caret.Line;
            }
            else if (caret.Line >= ScrollLine + visibleLineCount)
            {
                ScrollLine = caret.Line - visibleLineCount + 1;
            }

            ScrollLine = Math.Clamp(ScrollLine, 0, maximum);
        }

        EnsureCursorHorizontallyVisible(maximumWidth);
    }

    public int GetMaximumScrollX(int viewportWidth)
    {
        if (viewportWidth <= 0)
        {
            return 0;
        }

        var lines = Editor.Frame.Layout(viewportWidth);
        var maximumLineWidth = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            maximumLineWidth = Math.Max(maximumLineWidth, lines[index].Width);
        }

        return Math.Max(0, maximumLineWidth - viewportWidth);
    }

    public void ScrollHorizontally(int delta, int viewportWidth)
    {
        var maximum = GetMaximumScrollX(viewportWidth);
        ScrollX = Math.Clamp(ScrollX + delta, 0, maximum);
    }

    public void ClampScrollX(int viewportWidth)
    {
        ScrollX = Math.Clamp(ScrollX, 0, GetMaximumScrollX(viewportWidth));
    }

    private void EnsureCursorHorizontallyVisible(int viewportWidth)
    {
        var maximum = GetMaximumScrollX(viewportWidth);
        if (maximum == 0)
        {
            ScrollX = 0;
            return;
        }

        var caret = Editor.Frame.GetCaretLocation(
            Editor.Cursor,
            originX: 0,
            originY: 0,
            maximumWidth: viewportWidth,
            viewportHeight: 0,
            scrollLine: 0);
        if (caret.X < ScrollX)
        {
            ScrollX = caret.X;
        }
        else if (caret.X > ScrollX + viewportWidth - 1)
        {
            ScrollX = caret.X - viewportWidth + 1;
        }

        ScrollX = Math.Clamp(ScrollX, 0, maximum);
    }
}
