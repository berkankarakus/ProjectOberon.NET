using ProjectOberon.Emulator;

internal static partial class OberonViewerTests
{
    private static OberonViewerSystem CreateSystem()
    {
        var system = new OberonViewerSystem(400, 300);
        system.AddTrack(0, 200);
        system.AddTrack(200, 200);
        return system;
    }
}
