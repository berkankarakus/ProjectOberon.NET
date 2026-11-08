using ProjectOberon.Emulator;

internal static partial class OberonFileSystemTests
{
    private sealed class DiskFixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"project-oberon-fs-{Guid.NewGuid():N}.img");

        public DiskFixture()
        {
            using (var file = new FileStream(_path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            }

            Disk = new DiskImage();
            Disk.Open(_path);
        }

        public DiskImage Disk { get; }

        public void Dispose()
        {
            Disk.Dispose();
            File.Delete(_path);
        }
    }
}
