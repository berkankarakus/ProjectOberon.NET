using ProjectOberon.Emulator;

internal static class OberonModuleMemoryValidationTests
{
    public static void ValidatesLoaderAddresses()
    {
        var modules = new OberonModuleLoader(_ => null);
        var memory = new Memory();

        TestAssertions.Throws<ArgumentNullException>(() => new OberonModuleMemoryLoader(null!, modules));
        TestAssertions.Throws<ArgumentNullException>(() => new OberonModuleMemoryLoader(memory, null!));
        TestAssertions.Throws<ArgumentException>(() => new OberonModuleMemoryLoader(memory, modules, 0x1001));
        TestAssertions.Throws<ArgumentException>(() => new OberonModuleMemoryLoader(memory, modules, 0x1000, 0x101));
    }
}
