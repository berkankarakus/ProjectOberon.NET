namespace ProjectOberon.Emulator;

public sealed class OberonTextEditor
{
    private readonly Stack<EditorState> _undo = [];
    private readonly Stack<EditorState> _redo = [];

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

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

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

    public void MoveUp(int maximumWidth, bool extendSelection = false)
    {
        MoveVertical(-1, maximumWidth, extendSelection);
    }

    public void MoveDown(int maximumWidth, bool extendSelection = false)
    {
        MoveVertical(1, maximumWidth, extendSelection);
    }

    public void Insert(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return;
        }

        var style = Cursor < Text.Length ? Text.Attributes(Cursor) : OberonTextStyle.Default;
        Edit(() => ReplaceSelection(value, style));
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

        Edit(() =>
        {
            Text.Delete(Cursor - 1, Cursor);
            SetCursor(Cursor - 1);
        });
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
            Edit(() => Text.Delete(Cursor, Cursor + 1));
        }
    }

    public void DeleteSelection()
    {
        if (!HasSelection)
        {
            return;
        }

        Edit(DeleteSelectionCore);
    }

    public void ClearSelection() => SetCursor(Cursor);

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var current = CaptureState();
        RestoreState(_undo.Pop());
        _redo.Push(current);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var current = CaptureState();
        RestoreState(_redo.Pop());
        _undo.Push(current);
        return true;
    }

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

    private void MoveVertical(int direction, int maximumWidth, bool extendSelection)
    {
        if (maximumWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }

        var caret = Frame.GetCaretLocation(Cursor, 0, 0, maximumWidth);
        var lines = Frame.Layout(maximumWidth);
        var targetLine = Math.Clamp(caret.Line + direction, 0, lines.Count - 1);
        if (targetLine == caret.Line)
        {
            SetCursor(Cursor, extendSelection);
            return;
        }

        var targetY = targetLine * Frame.Font.Height + Frame.Font.Height / 2;
        var position = Frame.HitTest(caret.X, targetY, 0, 0, maximumWidth);
        SetCursor(position, extendSelection);
    }

    private void DeleteSelectionCore()
    {
        var start = SelectionStart;
        Text.Delete(start, SelectionEnd);
        SetCursor(start);
    }

    private void Edit(Action action)
    {
        var before = CaptureState();
        action();
        var after = CaptureState();
        if (StatesEqual(before, after))
        {
            return;
        }

        _undo.Push(before);
        _redo.Clear();
    }

    private EditorState CaptureState()
    {
        return new EditorState(Text.Copy(0, Text.Length), Cursor, Anchor);
    }

    private void RestoreState(EditorState state)
    {
        Text.Delete(0, Text.Length);
        Text.Insert(0, state.Buffer);
        SetCursor(state.Anchor);
        SetCursor(state.Cursor, extendSelection: true);
    }

    private static bool StatesEqual(EditorState left, EditorState right)
    {
        if (left.Cursor != right.Cursor || left.Anchor != right.Anchor || left.Buffer.Length != right.Buffer.Length)
        {
            return false;
        }

        if (left.Buffer.Pieces.Count != right.Buffer.Pieces.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Buffer.Pieces.Count; index++)
        {
            var leftPiece = left.Buffer.Pieces[index];
            var rightPiece = right.Buffer.Pieces[index];
            if (leftPiece.Text != rightPiece.Text || leftPiece.Style != rightPiece.Style)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record EditorState(OberonTextBuffer Buffer, int Cursor, int Anchor);
}
