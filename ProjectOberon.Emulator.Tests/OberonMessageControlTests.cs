using ProjectOberon.Emulator;

internal static partial class OberonMessageTests
{
    private static void RoutesAndBroadcastsControlMessages()
    {
        var received = new List<(string Name, OberonControlMessageKind Kind)>();
        var system = CreateSystem();
        var first = new OberonViewer("first", (_, message) => AddControlMessage(received, "first", message));
        var second = new OberonViewer("second", (_, message) => AddControlMessage(received, "second", message));
        system.Open(first, 10, 50);
        system.Open(second, 10, 90);
        received.Clear();

        TestAssertions.True(system.DispatchControl(10, 20, new OberonControlMessage(OberonControlMessageKind.Mark)));
        system.Broadcast(new OberonControlMessage(OberonControlMessageKind.Neutralize));

        TestAssertions.Equal(3, received.Count);
        TestAssertions.Equal(("first", OberonControlMessageKind.Mark), received[0]);
        TestAssertions.Equal(("first", OberonControlMessageKind.Neutralize), received[1]);
        TestAssertions.Equal(("second", OberonControlMessageKind.Neutralize), received[2]);
    }

    private static void AddControlMessage(
        List<(string Name, OberonControlMessageKind Kind)> messages,
        string name,
        OberonViewerMessage message)
    {
        if (message.Payload is OberonControlMessage control)
        {
            messages.Add((name, control.Kind));
        }
    }
}
