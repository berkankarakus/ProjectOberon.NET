using ProjectOberon.Emulator;

internal static partial class OberonTextFrameTests
{
    public static void ValidatesLayoutBounds()
    {
        var frame = new OberonTextFrame(new OberonText("A\rB\rC"), CreateFont());

        TestAssertions.Throws<ArgumentOutOfRangeException>(() => frame.Layout(0));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => frame.Layout(-1));
        TestAssertions.Equal(0, frame.GetMaxScrollLine(3, 24));
        TestAssertions.Equal(2, frame.GetMaxScrollLine(3, 8));
        TestAssertions.Equal(3, frame.VisibleLines(3, 0, 100).Count);
        TestAssertions.Equal("C", frame.VisibleLines(3, 8, 100)[0].Text);
    }

    private static void WrapsAndSplitsLines()
    {
        var frame = new OberonTextFrame(new OberonText("ABA\r\nBA"), CreateFont());
        var lines = frame.Layout(9);

        TestAssertions.Equal(2, lines.Count);
        TestAssertions.Equal("ABA", lines[0].Text);
        TestAssertions.Equal(0, lines[0].Start);
        TestAssertions.Equal("BA", lines[1].Text);
        TestAssertions.Equal(5, lines[1].Start);

        var wrapped = new OberonTextFrame(new OberonText("ABA"), CreateFont()).Layout(6);
        TestAssertions.Equal(2, wrapped.Count);
        TestAssertions.Equal("AB", wrapped[0].Text);
        TestAssertions.Equal("A", wrapped[1].Text);
    }
}
