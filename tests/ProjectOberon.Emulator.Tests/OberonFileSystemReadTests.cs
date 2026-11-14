using System.Text;
using ProjectOberon.Emulator;

internal static partial class OberonFileSystemTests
{
    private static void EnumeratesAndReadsPersistedFiles()
    {
        using var fixture = new DiskFixture();
        var kernel = new OberonKernel(new Memory(), fixture.Disk);
        var files = new OberonFiles(kernel, initializeDirectory: true);
        var file = files.New("Demo.Mod");
        file.Set().Write([1, 2, 3, 4]);
        files.Register(file);

        var fileSystem = new OberonFileSystem(fixture.Disk);
        var entries = fileSystem.Enumerate("Demo").ToArray();
        var content = fileSystem.ReadFile(entries.Single());

        TestAssertions.Equal(1, entries.Length);
        TestAssertions.Equal("Demo.Mod", entries[0].Name);
        TestAssertions.Equal(4, content.Length);
        TestAssertions.Equal((byte)1, content[0]);
        TestAssertions.Equal((byte)4, content[3]);
        fileSystem.Validate();
    }

    private static void DetectsMissingDataSectors()
    {
        using var fixture = new DiskFixture();
        var kernel = new OberonKernel(new Memory(), fixture.Disk);
        var headerAddress = kernel.AllocSector(0);
        var header = new byte[OberonDiskLayout.SectorLength];
        OberonFileHeaderCodec.Write(header, "Demo.Mod", allocatedPages: 1, lastPageLength: 1024,
            date: 0, extensions: [], pages: [headerAddress, 0]);
        kernel.PutSector(headerAddress, header);

        var fileSystem = new OberonFileSystem(fixture.Disk);

        TestAssertions.Throws<InvalidDataException>(() =>
            fileSystem.ReadFile(new OberonDirectoryEntry("Demo.Mod", headerAddress)));
        TestAssertions.Throws<InvalidDataException>(() => fileSystem.Validate());
    }

    private static void DetectsHeaderNameMismatches()
    {
        using var fixture = new DiskFixture();
        var kernel = new OberonKernel(new Memory(), fixture.Disk);
        var files = new OberonFiles(kernel, initializeDirectory: true);
        var file = files.New("Demo.Mod");
        file.Set().Write([1]);
        files.Register(file);

        var header = kernel.GetSector(file.HeaderAddress);
        Array.Clear(header, 4, 32);
        Encoding.ASCII.GetBytes("Other.Mod").CopyTo(header, 4);
        kernel.PutSector(file.HeaderAddress, header);

        var fileSystem = new OberonFileSystem(fixture.Disk);
        var error = TestAssertions.Throws<InvalidDataException>(() => fileSystem.Validate());
        TestAssertions.True(error.Message.Contains("directory name", StringComparison.Ordinal));
    }
}
