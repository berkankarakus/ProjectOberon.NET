using ProjectOberon.Emulator;

internal static partial class OberonViewerTests
{
    private static void OpensAndHitTestsVerticalViewerRegions()
    {
        var system = CreateSystem();
        var first = new OberonViewer("first");
        var second = new OberonViewer("second");

        TestAssertions.True(system.Open(first, 10, 100));
        TestAssertions.Equal(0, first.Y);
        TestAssertions.Equal(100, first.Height);
        TestAssertions.True(system.Open(second, 10, 250));
        TestAssertions.Equal(100, second.Y);
        TestAssertions.Equal(150, second.Height);
        TestAssertions.Equal(first, system.HitTest(10, 50));
        TestAssertions.Equal(second, system.HitTest(10, 200));
        TestAssertions.Equal(null, system.HitTest(10, 400));
    }

    private static void ClosesAndRecallsLastViewer()
    {
        var system = CreateSystem();
        var first = new OberonViewer("first");
        var second = new OberonViewer("second");
        system.Open(first, 10, 100);
        system.Open(second, 10, 250);

        TestAssertions.True(system.Close(first));
        TestAssertions.Equal(0, first.State);
        TestAssertions.Equal(first, system.Recall());
        TestAssertions.Equal(0, second.Y);
        TestAssertions.Equal(250, second.Height);
        TestAssertions.Equal(second, system.HitTest(10, 50));
    }

    private static void ChangesViewerHeightWithoutOverlappingNextViewer()
    {
        var system = CreateSystem();
        var first = new OberonViewer("first");
        var second = new OberonViewer("second");
        system.Open(first, 10, 100);
        system.Open(second, 10, 250);

        TestAssertions.True(system.Change(first, 150));
        TestAssertions.Equal(150, first.Height);
        TestAssertions.Equal(150, second.Y);
        TestAssertions.Equal(100, second.Height);
        TestAssertions.False(system.Change(first, 150));
    }

    private static void ClosesWholeTrack()
    {
        var system = CreateSystem();
        var first = new OberonViewer("first");
        var second = new OberonViewer("second");
        system.Open(first, 10, 100);
        system.Open(second, 10, 250);

        TestAssertions.Equal(2, system.CloseTrack(10));
        TestAssertions.Equal(0, system.Tracks[0].Viewers.Count);
        TestAssertions.Equal(null, system.HitTest(10, 50));
    }
}
