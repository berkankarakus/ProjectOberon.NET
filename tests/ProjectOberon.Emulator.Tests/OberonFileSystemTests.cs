internal static partial class OberonFileSystemTests
{
    public static void Run()
    {
        EnumeratesAndReadsPersistedFiles();
        RejectsReadOnlyMutations();
        DetectsDirectoryCycles();
        DetectsMissingDataSectors();
        RejectsAllocationPastDiskCapacity();
    }
}
