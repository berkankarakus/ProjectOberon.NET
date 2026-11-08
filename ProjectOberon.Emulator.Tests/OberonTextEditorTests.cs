using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static class OberonTextEditorTests
{
    public static void Run()
    {
        EditsAndSelectsText();
        MapsMouseToCursor();
        UndoesAndRedoesStyledEdits();
    }

    private static void EditsAndSelectsText()
    {
        var text = new OberonText("abc");
        var editor = new OberonTextEditor(new OberonTextFrame(text, CreateFont()));
        editor.SetCursor(1);
        editor.Insert("X");
        TestAssertions.Equal("aXbc", text.GetText());
        TestAssertions.Equal(2, editor.Cursor);

        editor.MoveRight(extendSelection: true);
        editor.MoveRight(extendSelection: true);
        TestAssertions.Equal("bc", editor.SelectedText);
        editor.Insert("YZ");
        TestAssertions.Equal("aXYZ", text.GetText());
        TestAssertions.Equal(4, editor.Cursor);

        editor.Backspace();
        TestAssertions.Equal("aXY", text.GetText());
        editor.MoveLeft();
        editor.Backspace();
        TestAssertions.Equal("aY", text.GetText());

        var lineEditor = new OberonTextEditor(new OberonTextFrame(new OberonText("ab\rcd"), CreateFont()));
        lineEditor.SetCursor(4);
        lineEditor.MoveHome();
        TestAssertions.Equal(3, lineEditor.Cursor);
        lineEditor.MoveEnd();
        TestAssertions.Equal(5, lineEditor.Cursor);
    }

    private static void MapsMouseToCursor()
    {
        var editor = new OberonTextEditor(new OberonTextFrame(new OberonText("AB"), CreateFont()));
        editor.SetCursorFromMouse(15, 20, 10, 20, 100);
        TestAssertions.Equal(2, editor.Cursor);
        var caret = editor.GetCaretLocation(10, 20, 100);
        TestAssertions.Equal(16, caret.X);
        TestAssertions.Equal(20, caret.Y);
    }

    private static void UndoesAndRedoesStyledEdits()
    {
        var text = new OberonText("abc");
        var editor = new OberonTextEditor(new OberonTextFrame(text, CreateFont()));
        editor.SetCursor(1);
        editor.Insert("X");
        editor.SetCursor(2, extendSelection: true);
        editor.Insert("Y");

        TestAssertions.True(editor.CanUndo);
        TestAssertions.True(editor.Undo());
        TestAssertions.Equal("aXbc", text.GetText());
        TestAssertions.Equal(2, editor.Cursor);
        TestAssertions.True(editor.Undo());
        TestAssertions.Equal("abc", text.GetText());
        TestAssertions.Equal(1, editor.Cursor);
        TestAssertions.False(editor.Undo());

        TestAssertions.True(editor.Redo());
        TestAssertions.Equal("aXbc", text.GetText());
        TestAssertions.True(editor.Redo());
        TestAssertions.Equal("aXYbc", text.GetText());
        TestAssertions.False(editor.Redo());
    }

    private static OberonFont CreateFont()
    {
        var data = new List<byte> { 0xdb, 0, 0x53, 0 };
        AddInt16(data, 8);
        AddInt16(data, 0);
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 7);
        AddInt16(data, 1);
        AddInt16(data, 'A');
        AddInt16(data, 'C');
        AddGlyph(data);
        AddGlyph(data);
        return OberonFont.Parse(data.ToArray());
    }

    private static void AddGlyph(List<byte> data)
    {
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 0);
        AddInt16(data, 2);
        AddInt16(data, 2);
        data.Add(1);
        data.Add(2);
    }

    private static void AddInt16(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)value));
        data.AddRange(bytes.ToArray());
    }
}
