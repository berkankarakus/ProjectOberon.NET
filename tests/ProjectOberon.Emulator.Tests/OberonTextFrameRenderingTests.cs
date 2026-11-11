using ProjectOberon.Emulator;

internal static partial class OberonTextFrameTests
{
    private static void RendersLinesToFramebuffer()
    {
        var memory = new Memory();
        var display = new OberonDisplay(memory);
        var frame = new OberonTextFrame(new OberonText("A\rA"), CreateFont());

        TestAssertions.Equal(36, frame.Render(display, 10, 20, 100, Display.White, Display.Replace));
        TestAssertions.True((memory.Read32(Display.Base + (uint)(20 * Display.RowBytes)) & (1u << 10)) != 0);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(28 * Display.RowBytes)) & (1u << 10)) != 0);
    }

    private static void RendersSelectionAndCaret()
    {
        var memory = new Memory();
        var display = new OberonDisplay(memory);
        var frame = new OberonTextFrame(new OberonText("AB"), CreateFont());

        frame.RenderSelection(display, 0, 1, 10, 20, 100);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(20 * Display.RowBytes)) & (1u << 10)) != 0);
        frame.RenderCaret(display, 2, 10, 20, 100);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(20 * Display.RowBytes)) & (1u << 16)) != 0);
    }
}
