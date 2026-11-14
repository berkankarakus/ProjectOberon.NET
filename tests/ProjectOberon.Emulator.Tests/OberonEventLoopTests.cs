using ProjectOberon.Emulator;

internal static class OberonEventLoopTests
{
    public static void Run()
    {
        FocusesViewerOnMouseAndDispatchesKeyboard();
        HandlesEscapeAndSetStarControlCharacters();
        SuppressesUnchangedIdleMouseEvents();
        DispatchesKeyCodesAndModifiers();
    }

    private static void FocusesViewerOnMouseAndDispatchesKeyboard()
    {
        var received = new List<(OberonViewerMessageKind Kind, object? Payload)>();
        var system = CreateSystem();
        var viewer = new OberonViewer("editor", (_, message) => received.Add((message.Kind, message.Payload)));
        system.Open(viewer, 10, 50);
        var loop = new OberonEventLoop(system);
        received.Clear();

        TestAssertions.True(loop.ProcessMouse(keys: 1, x: 10, y: 20));
        TestAssertions.Equal(viewer, system.FocusViewer);
        TestAssertions.True(loop.ProcessConsumedCharacter('A'));
        TestAssertions.Equal(2, received.Count);
        TestAssertions.Equal(OberonViewerMessageKind.Input, received[1].Kind);
        TestAssertions.Equal('A', ((OberonInputMessage)received[1].Payload!).Character);
    }

    private static void HandlesEscapeAndSetStarControlCharacters()
    {
        var controls = new List<OberonControlMessageKind>();
        var system = CreateSystem();
        var viewer = new OberonViewer("editor", (_, message) =>
        {
            if (message.Payload is OberonControlMessage control)
            {
                controls.Add(control.Kind);
            }
        });
        system.Open(viewer, 10, 50);
        var loop = new OberonEventLoop(system);

        TestAssertions.True(loop.ProcessCharacter(OberonEventLoop.Escape, 10, 20));
        TestAssertions.True(loop.ProcessCharacter(OberonEventLoop.SetStar, 10, 20));
        TestAssertions.Equal(OberonControlMessageKind.Neutralize, controls[0]);
        TestAssertions.Equal(OberonControlMessageKind.Mark, controls[1]);
    }

    private static void SuppressesUnchangedIdleMouseEvents()
    {
        var received = 0;
        var system = CreateSystem();
        var viewer = new OberonViewer("editor", (_, message) =>
        {
            if (message.Payload is OberonInputMessage input && input.Kind == OberonInputMessageKind.Track)
            {
                received++;
            }
        });
        system.Open(viewer, 10, 50);
        var loop = new OberonEventLoop(system);

        TestAssertions.True(loop.ProcessIdleMouse(10, 20));
        TestAssertions.False(loop.ProcessIdleMouse(10, 20));
        TestAssertions.True(loop.ProcessIdleMouse(11, 20));
        TestAssertions.Equal(2, received);
    }

    private static void DispatchesKeyCodesAndModifiers()
    {
        OberonInputMessage? received = null;
        var system = CreateSystem();
        var viewer = new OberonViewer("editor", (_, message) =>
        {
            if (message.Payload is OberonInputMessage input)
            {
                received = input;
                input.Handled = true;
            }
        });
        system.Open(viewer, 10, 50);
        TestAssertions.True(system.PassFocus(viewer));
        var loop = new OberonEventLoop(system);

        TestAssertions.True(loop.ProcessConsumedKey(0x75, 0x0002));
        TestAssertions.Equal(0x75, received!.KeyCode);
        TestAssertions.Equal((ushort)0x0002, received.Modifiers);
        TestAssertions.Equal(OberonInputMessageKind.Consume, received.Kind);
    }

    private static OberonViewerSystem CreateSystem()
    {
        var system = new OberonViewerSystem(200, 100);
        system.AddTrack(0, 200);
        return system;
    }
}
