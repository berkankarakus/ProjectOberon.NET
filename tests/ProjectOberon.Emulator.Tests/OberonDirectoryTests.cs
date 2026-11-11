using ProjectOberon.Emulator;

internal static partial class OberonPersistenceTests
{
    public static void OberonFileDirectoryBTree()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-directory-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            using var disk = new DiskImage();
            disk.Open(path);
            var kernel = new OberonKernel(new Memory(), disk);
            var directory = new OberonFileDirectory(kernel);
            directory.Initialize();
            for (var index = 0; index < 25; index++) directory.Insert($"File{index:00}", (100 + index) * OberonKernel.LogicalSectorUnit);
            TestAssertions.Equal(25, directory.Enumerate().Count());
            TestAssertions.Equal(112 * OberonKernel.LogicalSectorUnit, directory.Search("File12"));
            TestAssertions.True(directory.Rename("File12", "Renamed"));
            TestAssertions.Equal(112 * OberonKernel.LogicalSectorUnit, directory.Search("Renamed"));
            TestAssertions.Equal(0, directory.Search("File12"));
            TestAssertions.True(directory.Delete("Renamed", out var removed));
            TestAssertions.Equal(112 * OberonKernel.LogicalSectorUnit, removed);
            TestAssertions.Equal(24, directory.Enumerate().Count());
            for (var index = 0; index < 12; index++) TestAssertions.True(directory.Delete($"File{index:00}", out _));
            TestAssertions.Equal(12, directory.Enumerate().Count());
        }
        finally { File.Delete(path); }
    }

    public static void OberonFileDirectoryBoundaries()
    {
        WithInitializedDirectory((directory, kernel) =>
        {
            const int count = 73;
            for (var index = count - 1; index >= 0; index--)
            {
                directory.Insert($"Item{index:00}", (1000 + index) * OberonKernel.LogicalSectorUnit);
            }

            var entries = directory.Enumerate().ToArray();
            TestAssertions.Equal(count, entries.Length);
            for (var index = 0; index < count; index++)
            {
                TestAssertions.Equal($"Item{index:00}", entries[index].Name);
                TestAssertions.Equal((1000 + index) * OberonKernel.LogicalSectorUnit,
                    directory.Search($"Item{index:00}"));
            }

            var prefixEntries = directory.Enumerate("Item0").ToArray();
            TestAssertions.Equal(10, prefixEntries.Length);
            TestAssertions.Equal("Item09", prefixEntries[^1].Name);
            TestAssertions.False(directory.Delete("Missing", out var missingAddress));
            TestAssertions.Equal(0, missingAddress);
            TestAssertions.False(directory.Rename("Missing", "Other"));

            for (var index = count - 1; index >= 0; index--)
            {
                TestAssertions.True(directory.Delete($"Item{index:00}", out var removed));
                TestAssertions.Equal((1000 + index) * OberonKernel.LogicalSectorUnit, removed);
            }

            TestAssertions.Equal(0, directory.Enumerate().Count());
            var root = OberonDirectoryPageCodec.Read(kernel.GetSector(OberonFileDirectory.RootAddress),
                OberonFileDirectory.RootAddress);
            TestAssertions.Equal(0, root.Entries.Count);
            TestAssertions.Equal(1, root.Children.Count);
            TestAssertions.Equal(0, root.Children[0]);
        });
    }

    private static void WithInitializedDirectory(Action<OberonFileDirectory, OberonKernel> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-directory-boundary-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            using var disk = new DiskImage();
            disk.Open(path);
            var kernel = new OberonKernel(new Memory(), disk);
            var directory = new OberonFileDirectory(kernel);
            directory.Initialize();
            action(directory, kernel);
        }
        finally { File.Delete(path); }
    }
}
