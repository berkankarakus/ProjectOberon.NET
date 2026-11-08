internal static partial class OberonViewerTests
{
    public static void Run()
    {
        OpensAndHitTestsVerticalViewerRegions();
        ClosesAndRecallsLastViewer();
        ChangesViewerHeightWithoutOverlappingNextViewer();
        DispatchesInputAndBroadcastsMessages();
        ClosesWholeTrack();
    }
}
