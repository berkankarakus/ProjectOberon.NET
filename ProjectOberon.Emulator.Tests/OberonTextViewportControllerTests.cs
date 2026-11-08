using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static class OberonTextViewportControllerTests
{
    public static void Run()
    {
        KeepsTheCursorInsideTheVisibleLineRange();
        ClampsScrollChangesToViewportBounds();
    }

    private static void KeepsTheCursorInsideTheVisibleLineRange()
    {
        var editor = new OberonTextEditor(
            new OberonTextFrame(new OberonText("A\rA\rA"), CreateFont()));
        var viewport = new OberonTextViewportController(editor);

        editor.SetCursor(editor.Text.Length);
        viewport.EnsureCursorVisible(maximumWidth: 100, viewportHeight: 16);

        TestAssertions.Equal(1, viewport.ScrollLine);
        TestAssertions.Equal(1, viewport.GetMaximumScrollLine(100, 16));
    }

    private static void ClampsScrollChangesToViewportBounds()
    {
        var editor = new OberonTextEditor(
            new OberonTextFrame(new OberonText("A\rA\rA"), CreateFont()));
        var viewport = new OberonTextViewportController(editor);

        viewport.ScrollBy(100, 100, 16);
        TestAssertions.Equal(1, viewport.ScrollLine);
        viewport.ScrollBy(-100, 100, 16);
        TestAssertions.Equal(0, viewport.ScrollLine);
        viewport.ScrollBy(100, 0, 16);
        TestAssertions.Equal(0, viewport.ScrollLine);
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
