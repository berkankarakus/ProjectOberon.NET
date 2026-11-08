internal static partial class OberonTextFrameViewerTests
{
    public static void Run()
    {
        ConnectsTextFrameToMenuViewerViewport();
        RoutesMainInputToTextSelection();
        ResizesAndKeepsCursorVisible();
        ConsumesFocusedCharacters();
    }
}
