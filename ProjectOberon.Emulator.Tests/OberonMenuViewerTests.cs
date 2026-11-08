using ProjectOberon.Emulator;

internal static class OberonMenuViewerTests
{
    public static void Run()
    {
        LaysOutMenuAndMainFrames();
        RoutesInputToTheCorrectChildFrame();
        ResizesAndReportsExtendOrReduce();
        SuspendsChildFramesWhenClosed();
    }

    private static void LaysOutMenuAndMainFrames()
    {
        var system = CreateSystem();
        var menu = new OberonViewerFrame("menu");
        var main = new OberonViewerFrame("main");
        var viewer = new OberonMenuViewer(system, "editor", menu, main, 20, 10, 200);

        TestAssertions.Equal(0, viewer.Viewer.X);
        TestAssertions.Equal(0, viewer.Viewer.Y);
        TestAssertions.Equal(200, viewer.Viewer.Height);
        TestAssertions.Equal(1, main.X);
        TestAssertions.Equal(1, main.Y);
        TestAssertions.Equal(178, main.Height);
        TestAssertions.Equal(1, menu.X);
        TestAssertions.Equal(179, menu.Y);
        TestAssertions.Equal(20, menu.Height);
    }

    private static void RoutesInputToTheCorrectChildFrame()
    {
        var received = new List<(string Name, OberonFrameMessageKind Kind, int X, int Y)>();
        var system = CreateSystem();
        var menu = new OberonViewerFrame("menu", (frame, message) =>
            received.Add((frame.Name, message.Kind, message.X, message.Y)));
        var main = new OberonViewerFrame("main", (frame, message) =>
            received.Add((frame.Name, message.Kind, message.X, message.Y)));
        var viewer = new OberonMenuViewer(system, "editor", menu, main, 20, 10, 200);
        received.Clear();

        TestAssertions.True(viewer.DispatchInput(20, 50));
        TestAssertions.True(viewer.DispatchInput(20, 190));

        TestAssertions.Equal(2, received.Count);
        TestAssertions.Equal(("main", OberonFrameMessageKind.Input, 20, 50), received[0]);
        TestAssertions.Equal(("menu", OberonFrameMessageKind.Input, 20, 190), received[1]);
    }

    private static void ResizesAndReportsExtendOrReduce()
    {
        var received = new List<OberonFrameMessageKind>();
        var system = CreateSystem();
        var menu = new OberonViewerFrame("menu", (_, message) => received.Add(message.Kind));
        var main = new OberonViewerFrame("main", (_, message) => received.Add(message.Kind));
        var viewer = new OberonMenuViewer(system, "editor", menu, main, 20, 10, 200);
        received.Clear();

        TestAssertions.True(viewer.DispatchInput(20, 190, resizeRequested: true));
        TestAssertions.Equal(190, viewer.Viewer.Height);
        TestAssertions.Equal(168, main.Height);
        TestAssertions.Equal(20, menu.Height);
        TestAssertions.True(received.Contains(OberonFrameMessageKind.Reduce));

        received.Clear();
        TestAssertions.True(viewer.Resize(240));
        TestAssertions.Equal(240, viewer.Viewer.Height);
        TestAssertions.True(received.Contains(OberonFrameMessageKind.Extend));
    }

    private static void SuspendsChildFramesWhenClosed()
    {
        var system = CreateSystem();
        var menu = new OberonViewerFrame("menu");
        var main = new OberonViewerFrame("main");
        var viewer = new OberonMenuViewer(system, "editor", menu, main, 20, 10, 200);

        TestAssertions.True(system.Close(viewer.Viewer));
        TestAssertions.Equal(0, menu.Height);
        TestAssertions.Equal(0, main.Height);
        TestAssertions.Equal(0, viewer.Viewer.State);
    }

    private static OberonViewerSystem CreateSystem()
    {
        var system = new OberonViewerSystem(400, 300);
        system.AddTrack(0, 200);
        return system;
    }
}
