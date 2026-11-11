using ProjectOberon.Emulator;

internal static class OberonFileNameTests
{
    public static void Run()
    {
        AcceptsValidNamesThroughPublicFacade();
        RejectsInvalidNamesConsistently();
        SupportsUnnamedTemporaryFiles();
    }

    private static void AcceptsValidNamesThroughPublicFacade()
    {
        OberonFiles.ValidateName("Kernel");
        OberonFiles.ValidateName("Files.Mod");
        OberonFiles.ValidateName("A123");
    }

    private static void RejectsInvalidNamesConsistently()
    {
        foreach (var name in new[]
        {
            "", ".Mod", "1Module", "A/B", "A B", "A\nB", "Módulo", new string('x', 32)
        })
        {
            TestAssertions.Throws<ArgumentException>(() => OberonFiles.ValidateName(name));
        }
    }

    private static void SupportsUnnamedTemporaryFiles()
    {
        using var disk = new DiskImage();
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-empty-file-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            }

            disk.Open(path);
            using var machine = new Machine();
            var kernel = new OberonKernel(machine.Memory, disk);
            var files = new OberonFiles(kernel, initializeDirectory: true);

            TestAssertions.True(files.Old("") is null);
            var temporary = files.New("");
            TestAssertions.Equal(string.Empty, temporary.Name);
            TestAssertions.Equal(0, temporary.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
