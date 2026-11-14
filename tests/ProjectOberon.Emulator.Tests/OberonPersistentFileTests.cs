using ProjectOberon.Emulator;

internal static partial class OberonPersistenceTests
{
    public static void OberonPersistentFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-files-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            using var disk = new DiskImage();
            disk.Open(path);
            var kernel = new OberonKernel(new Memory(), disk);
            var files = new OberonFiles(kernel, initializeDirectory: true);
            var text = new OberonText("Persistent text");
            text.ChangeLooks(0, 10, new OberonTextStyle("Oberon12", 15, 0));
            files.RegisterText("Note.Text", text, date: 7);
            TestAssertions.Equal("Persistent text", files.OpenText("Note.Text").GetText());
            var created = files.New("Persist.Mod");
            var writer = created.Set();
            writer.WriteString("Project Oberon");
            for (var index = 0; index < 1500; index++) writer.WriteByte((byte)(index % 251));
            files.Register(created);
            var loaded = files.Old("Persist.Mod");
            TestAssertions.True(loaded is not null);
            TestAssertions.Equal(created.Length, loaded!.Length);
            var reader = loaded.Set();
            TestAssertions.Equal("Project Oberon", reader.ReadString());
            for (var index = 0; index < 1500; index++)
            {
                TestAssertions.True(reader.ReadByte(out var value));
                TestAssertions.Equal((byte)(index % 251), value);
            }
            TestAssertions.False(loaded.Writable);
            var reopenedKernel = new OberonKernel(new Memory(), disk);
            var reopenedFiles = new OberonFiles(reopenedKernel);
            TestAssertions.True(reopenedFiles.Old("Persist.Mod") is not null);
            var reopenedText = reopenedFiles.OpenText("Note.Text");
            TestAssertions.Equal("Persistent text", reopenedText.GetText());
            TestAssertions.Equal(new OberonTextStyle("Oberon12", 15, 0), reopenedText.Attributes(0));
            reopenedFiles.RegisterText("Existing.Text", new OberonText("existing"));
            reopenedFiles.RegisterText("Incoming.Text", new OberonText("incoming"));
            TestAssertions.False(reopenedFiles.Rename("Existing.Text", "Incoming.Text"));
            TestAssertions.True(reopenedFiles.Old("Existing.Text") is not null);
            TestAssertions.Equal("incoming", reopenedFiles.OpenText("Incoming.Text").GetText());
            TestAssertions.True(reopenedFiles.Rename("Persist.Mod", "Renamed.Mod"));
            TestAssertions.True(reopenedFiles.Delete("Renamed.Mod"));
            TestAssertions.True(reopenedFiles.Old("Renamed.Mod") is null);
        }
        finally { File.Delete(path); }
    }

    public static void RollsBackFailedFileAllocation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-files-capacity-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
                file.SetLength(((long)OberonKernel.FileSystemOffset + 2 * 65) * DiskImage.SectorSize);
            using var disk = new DiskImage();
            disk.Open(path);
            var kernel = new OberonKernel(new Memory(), disk);
            var files = new OberonFiles(kernel, initializeDirectory: true);
            var oversized = files.New("TooLarge.Mod");
            oversized.Set().WriteString(new string('x', 700));

            TestAssertions.Throws<InvalidOperationException>(() => files.Register(oversized));
            TestAssertions.Equal(0, kernel.AllocatedSectors);
            TestAssertions.True(files.Old("TooLarge.Mod") is null);
        }
        finally { File.Delete(path); }
    }
}
