using ProjectOberon.Emulator;

internal static partial class OberonMessageTests
{
    private static void DispatchesTypedInputToTheHitViewer()
    {
        OberonInputMessage? received = null;
        var system = CreateSystem();
        var viewer = new OberonViewer("editor", (_, message) =>
        {
            if (message.Payload is OberonInputMessage input)
            {
                received = input;
            }
        });
        system.Open(viewer, 10, 50);

        var input = new OberonInputMessage(OberonInputMessageKind.Track)
        {
            X = 10,
            Y = 20,
            Keys = 4
        };

        TestAssertions.True(system.DispatchInput(10, 20, input));
        TestAssertions.Equal(input, received);
    }
}
