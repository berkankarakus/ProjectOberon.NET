using ProjectOberon.Emulator;

internal static partial class OberonViewerTests
{
    private static void DispatchesInputAndBroadcastsMessages()
    {
        var received = new List<(string Name, OberonViewerMessageKind Kind)>();
        var system = CreateSystem();
        var first = new OberonViewer("first", (viewer, message) => received.Add((viewer.Name, message.Kind)));
        var second = new OberonViewer("second", (viewer, message) => received.Add((viewer.Name, message.Kind)));
        system.Open(first, 10, 100);
        system.Open(second, 10, 250);
        received.Clear();

        TestAssertions.True(system.DispatchInput(10, 200, new OberonViewerMessage(OberonViewerMessageKind.Input)));
        system.Broadcast(new OberonViewerMessage(OberonViewerMessageKind.Control));

        TestAssertions.Equal(3, received.Count);
        TestAssertions.Equal(("second", OberonViewerMessageKind.Input), received[0]);
        TestAssertions.Equal(("first", OberonViewerMessageKind.Control), received[1]);
        TestAssertions.Equal(("second", OberonViewerMessageKind.Control), received[2]);
    }
}
