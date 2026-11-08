using ProjectOberon.Emulator;

internal static class OberonFileNameTests
{
    public static void Run()
    {
        AcceptsValidNamesThroughPublicFacade();
        RejectsInvalidNamesConsistently();
    }

    private static void AcceptsValidNamesThroughPublicFacade()
    {
        OberonFiles.ValidateName("Kernel");
        OberonFiles.ValidateName("Files.Mod");
        OberonFiles.ValidateName("A123");
    }

    private static void RejectsInvalidNamesConsistently()
    {
        foreach (var name in new[] { "", ".Mod", "A/B", "A B", new string('x', 32) })
        {
            TestAssertions.Throws<ArgumentException>(() => OberonFiles.ValidateName(name));
        }
    }
}
