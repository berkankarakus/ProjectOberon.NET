using ProjectOberon.Emulator;

internal static partial class OberonTextFrameTests
{
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
