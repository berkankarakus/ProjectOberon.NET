using ProjectOberon.Emulator;

internal static partial class OberonMessageTests
{
    private static OberonViewer CreateRecordingViewer(
        string name,
        List<(string Name, OberonControlMessageKind Kind)> controlMessages,
        List<(string Name, char Character)> keyboardMessages)
    {
        return new OberonViewer(name, (_, message) =>
        {
            if (message.Payload is OberonControlMessage control)
            {
                controlMessages.Add((name, control.Kind));
            }
            else if (message.Payload is OberonInputMessage input && input.Kind == OberonInputMessageKind.Consume)
            {
                keyboardMessages.Add((name, input.Character));
            }
        });
    }

    private static OberonViewerSystem CreateSystem()
    {
        var system = new OberonViewerSystem(200, 100);
        system.AddTrack(0, 200);
        return system;
    }
}
