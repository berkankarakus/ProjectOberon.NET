using ProjectOberon.Emulator;

internal static partial class OberonViewerTests
{
    private static void ValidatesViewerSystemBounds()
    {
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => new OberonViewerSystem(0, 100));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => new OberonViewerSystem(100, 0));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => new OberonViewerSystem(100, 100, 0));

        var system = new OberonViewerSystem(100, 100);
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => system.AddTrack(-1, 10));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => system.AddTrack(0, 0));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => system.AddTrack(90, 11));
    }
}
