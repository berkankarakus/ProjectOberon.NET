using ProjectOberon.Emulator;

internal static partial class OberonFileSystemTests
{
    private static void RejectsReadOnlyMutations()
    {
        using var fixture = new DiskFixture();
        var fileSystem = new OberonFileSystem(fixture.Disk);
        var files = new OberonFiles(fileSystem);

        TestAssertions.Throws<InvalidOperationException>(() => files.Register(new OberonFile("Demo.Mod", [])));
        TestAssertions.Throws<InvalidOperationException>(() => files.RegisterText("Demo.Text", new OberonText("text")));
        TestAssertions.Throws<InvalidOperationException>(() => files.Delete("Demo.Mod"));
        TestAssertions.Throws<InvalidOperationException>(() => files.Rename("Demo.Mod", "Other.Mod"));
    }

    private static void DetectsDirectoryCycles()
    {
        using var fixture = new DiskFixture();
        var kernel = new OberonKernel(new Memory(), fixture.Disk);
        kernel.PutSector(OberonFileDirectory.RootAddress, OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Children = [OberonFileDirectory.RootAddress]
        }));

        var fileSystem = new OberonFileSystem(fixture.Disk);

        TestAssertions.Throws<InvalidDataException>(() => fileSystem.Enumerate().ToArray());
    }

    private static void RejectsAllocationPastDiskCapacity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-capacity-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 2 * 65) * DiskImage.SectorSize);
            }

            using var disk = new DiskImage();
            disk.Open(path);
            var kernel = new OberonKernel(new Memory(), disk);
            TestAssertions.Equal(64 * OberonKernel.LogicalSectorUnit, kernel.AllocSector(0));
            TestAssertions.Throws<InvalidOperationException>(() => kernel.AllocSector(0));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
