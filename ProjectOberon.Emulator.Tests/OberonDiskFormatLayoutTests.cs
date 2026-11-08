using ProjectOberon.Emulator;

internal static partial class OberonDiskFormatTests
{
    private static void MapsLogicalSectorsToPhysicalSectors()
    {
        TestAssertions.Equal(0x80000u, OberonDiskLayout.PhysicalSector(0));
        TestAssertions.Equal(0x80002u, OberonDiskLayout.PhysicalSector(29));
        TestAssertions.Equal(0x80004u, OberonDiskLayout.PhysicalSector(58));
    }

    private static void RejectsInvalidLogicalAddresses()
    {
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => OberonDiskLayout.PhysicalSector(-1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => OberonDiskLayout.PhysicalSector(1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => OberonDiskLayout.PhysicalSector(30));
    }
}
