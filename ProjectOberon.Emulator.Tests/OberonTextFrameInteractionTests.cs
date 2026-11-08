using ProjectOberon.Emulator;

internal static partial class OberonTextFrameTests
{
    private static void MapsCaretAndMouseCoordinates()
    {
        var frame = new OberonTextFrame(new OberonText("AB\rBA"), CreateFont());
        var caret = frame.GetCaretLocation(3, 10, 20, 100);

        TestAssertions.Equal(10, caret.X);
        TestAssertions.Equal(28, caret.Y);
        TestAssertions.Equal(1, caret.Line);
        TestAssertions.Equal(2, frame.HitTest(15, 20, 10, 20, 100));
        TestAssertions.Equal(3, frame.HitTest(10, 28, 10, 20, 100));
        TestAssertions.Equal(5, frame.HitTest(100, 28, 10, 20, 100));
    }

    private static void ScrollsViewportAndMapsCoordinates()
    {
        var memory = new Memory();
        var display = new OberonDisplay(memory);
        var frame = new OberonTextFrame(new OberonText("A\rA\rA"), CreateFont());

        TestAssertions.Equal(1, frame.GetMaxScrollLine(100, 16));
        TestAssertions.Equal(1, frame.VisibleLines(100, 8, 1).Count);
        TestAssertions.Equal(2, frame.VisibleLines(100, 8, 1)[0].Start);
        frame.Render(display, 0, 0, 100, Display.White, Display.Replace, viewportHeight: 8, scrollLine: 1);
        TestAssertions.True((memory.Read32(Display.Base) & 1u) != 0);
        TestAssertions.Equal(0u, memory.Read32(Display.Base + (uint)(8 * Display.RowBytes)));

        var caret = frame.GetCaretLocation(4, 0, 0, 100, viewportHeight: 8, scrollLine: 1);
        TestAssertions.Equal(8, caret.Y);
        TestAssertions.Equal(2, caret.Line);
        TestAssertions.Equal(2, frame.HitTest(0, 0, 0, 0, 100, viewportHeight: 8, scrollLine: 1));
    }
}
