internal static partial class OberonTextFrameTests
{
    public static void Run()
    {
        WrapsAndSplitsLines();
        RendersLinesToFramebuffer();
        MapsCaretAndMouseCoordinates();
        RendersSelectionAndCaret();
        ScrollsViewportAndMapsCoordinates();
        ReusesLayoutUntilTextChanges();
        InvalidatesLayoutWhenWrapModeChanges();
    }
}
