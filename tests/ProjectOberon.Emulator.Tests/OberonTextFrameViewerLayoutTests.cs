using ProjectOberon.Emulator;

internal static partial class OberonTextFrameViewerTests
{
    private static void ConnectsTextFrameToMenuViewerViewport()
    {
        var memory = new Memory();
        var display = new OberonDisplay(memory);
        var system = CreateSystem();
        var viewer = new OberonTextFrameViewer(
            system,
            "editor",
            new OberonTextFrame(new OberonText("A\rA"), CreateFont()),
            menuHeight: 10,
            x: 0,
            y: 100);

        TestAssertions.Equal(88, viewer.Viewer.Main.Height);
        TestAssertions.Equal(10, viewer.Viewer.Menu.Height);
        TestAssertions.Equal(1, viewer.Viewer.Main.X);
        TestAssertions.Equal(1, viewer.Viewer.Main.Y);
        TestAssertions.Equal(89, viewer.Render(display));
        TestAssertions.True((memory.Read32(Display.Base + (uint)((viewer.Viewer.Main.Y + 1) * Display.RowBytes)) & 4u) != 0);
    }

    private static void RoutesMainInputToTextSelection()
    {
        var system = CreateSystem();
        var viewer = new OberonTextFrameViewer(
            system,
            "editor",
            new OberonTextFrame(new OberonText("AB"), CreateFont()),
            menuHeight: 10,
            x: 0,
            y: 100);

        TestAssertions.True(viewer.Viewer.DispatchInput(4, 5));
        TestAssertions.Equal(1, viewer.Editor.Cursor);
        TestAssertions.True(viewer.SetCursorFromPoint(1, 1, extendSelection: true));
        TestAssertions.Equal("A", viewer.Editor.SelectedText);
        TestAssertions.False(viewer.SetCursorFromPoint(2, viewer.Viewer.Menu.Y));
    }
}
