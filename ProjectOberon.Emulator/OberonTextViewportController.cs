namespace ProjectOberon.Emulator;

public sealed class OberonTextViewportController
{
    public OberonTextViewportController(OberonTextEditor editor)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public OberonTextEditor Editor { get; }

    public int ScrollLine { get; private set; }

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
            return;
        }

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
}
