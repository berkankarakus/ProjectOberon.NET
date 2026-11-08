namespace ProjectOberon.Emulator;

public sealed class OberonTextEditor
{
    public OberonTextEditor(OberonTextFrame frame)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public OberonTextFrame Frame { get; }
    public OberonText Text => Frame.Text;
    public int Cursor { get; private set; }
    public int Anchor { get; private set; }
    public bool HasSelection => Cursor != Anchor;
    public int SelectionStart => Math.Min(Cursor, Anchor);
    public int SelectionEnd => Math.Max(Cursor, Anchor);
    public string SelectedText => HasSelection ? Text.GetText()[SelectionStart..SelectionEnd] : string.Empty;

    public void SetCursor(int position, bool extendSelection = false)
    {
        if (position < 0 || position > Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        Cursor = position;
        if (!extendSelection)
        {
            Anchor = position;
        }
    }

    public void SetCursorFromMouse(int x, int y, int originX, int originY, int maximumWidth, bool extendSelection = false)
    {
        SetCursor(Frame.HitTest(x, y, originX, originY, maximumWidth), extendSelection);
    }

    public OberonCaretLocation GetCaretLocation(int originX, int originY, int maximumWidth)
    {
        return Frame.GetCaretLocation(Cursor, originX, originY, maximumWidth);
    }

    public void MoveLeft(bool extendSelection = false)
    {
        SetCursor(Math.Max(0, Cursor - 1), extendSelection);
    }

    public void MoveRight(bool extendSelection = false)
    {
        SetCursor(Math.Min(Text.Length, Cursor + 1), extendSelection);
    }

    public void MoveHome(bool extendSelection = false)
    {
        var text = Text.GetText();
        var position = Cursor;
        while (position > 0 && text[position - 1] is not '\r' and not '\n')
        {
            position--;
        }

        SetCursor(position, extendSelection);
    }

    public void MoveEnd(bool extendSelection = false)
    {
        var text = Text.GetText();
        var position = Cursor;
        while (position < text.Length && text[position] is not '\r' and not '\n')
        {
            position++;
        }

        SetCursor(position, extendSelection);
    }

    public void Insert(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return;
        }

        var style = Cursor < Text.Length ? Text.Attributes(Cursor) : OberonTextStyle.Default;
        ReplaceSelection(value, style);
    }

    public void Backspace()
    {
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }
        if (Cursor == 0)
        {
            return;
        }

        Text.Delete(Cursor - 1, Cursor);
        SetCursor(Cursor - 1);
    }

    public void Delete()
    {
        if (HasSelection)
        {
            DeleteSelection();
            return;
        }
        if (Cursor < Text.Length)
        {
            Text.Delete(Cursor, Cursor + 1);
        }
    }

    public void DeleteSelection()
    {
        if (!HasSelection)
        {
            return;
        }

        var start = SelectionStart;
        Text.Delete(start, SelectionEnd);
        SetCursor(start);
    }

    public void ClearSelection() => SetCursor(Cursor);

    private void ReplaceSelection(string value, OberonTextStyle style)
    {
        var start = SelectionStart;
        if (HasSelection)
        {
            Text.Delete(start, SelectionEnd);
        }

        var buffer = new OberonTextBuffer();
        buffer.Append(value, style);
        Text.Insert(start, buffer);
        SetCursor(start + value.Length);
    }
}
