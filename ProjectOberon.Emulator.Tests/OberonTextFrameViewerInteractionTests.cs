using ProjectOberon.Emulator;

internal static partial class OberonTextFrameViewerTests
{
    private static void ResizesAndKeepsCursorVisible()
    {
        var system = CreateSystem();
        var text = string.Join('\r', Enumerable.Repeat("A", 10));
        var viewer = new OberonTextFrameViewer(
            system,
            "editor",
            new OberonTextFrame(new OberonText(text), CreateFont()),
            menuHeight: 10,
            x: 0,
            y: 200);

        TestAssertions.True(viewer.Resize(40));
        TestAssertions.Equal(40, viewer.Viewer.Viewer.Height);
        viewer.SetCursor(viewer.Editor.Text.Length);
        TestAssertions.True(viewer.ScrollLine > 0);
        viewer.ScrollBy(-100);
        TestAssertions.Equal(0, viewer.ScrollLine);
    }

    private static void ConsumesFocusedCharacters()
    {
        var system = CreateSystem();
        var viewer = new OberonTextFrameViewer(
            system,
            "editor",
            new OberonTextFrame(new OberonText("A"), CreateFont()),
            menuHeight: 10,
            x: 0,
            y: 100);

        TestAssertions.True(viewer.Focus());
        TestAssertions.True(system.DispatchKeyboard(new OberonInputMessage(OberonInputMessageKind.Consume)
        {
            Character = 'B'
        }));
        TestAssertions.Equal("BA", viewer.Editor.Text.GetText());
    }
}
