using ProjectOberon.Emulator;

internal static partial class DeviceTests
{
    public static void DiskImageReadWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-csharp-{Guid.NewGuid():N}.img");
        try
        {
            File.WriteAllBytes(path, new byte[DiskImage.SectorSize * 2]);
            using (var disk = new DiskImage())
            {
                disk.Open(path);
                var reads = 0;
                disk.SectorRead += sectorNumber =>
                {
                    TestAssertions.Equal(1u, sectorNumber);
                    reads++;
                };
                var sector = new byte[DiskImage.SectorSize];
                sector[0] = 0xa5;
                disk.Write(1, sector);
                TestAssertions.Equal(0xa5, disk.Read(1)[0]);
                TestAssertions.Equal(1, reads);
            }
            using (var readOnly = new DiskImage())
            {
                readOnly.Open(path, writable: false);
                TestAssertions.Throws<InvalidOperationException>(() => readOnly.Write(0, new byte[DiskImage.SectorSize]));
            }
        }
        finally { File.Delete(path); }
    }

    public static void DiskImageValidatesLifecycle()
    {
        using var disk = new DiskImage();
        TestAssertions.Throws<InvalidOperationException>(() => disk.Read(0));

        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-invalid-disk-{Guid.NewGuid():N}.img");
        try
        {
            File.WriteAllBytes(path, [1]);
            TestAssertions.Throws<InvalidDataException>(() => disk.Open(path));
            TestAssertions.False(disk.Writable);
            TestAssertions.Equal(0u, disk.SectorCount);

            File.WriteAllBytes(path, new byte[DiskImage.SectorSize]);
            disk.Open(path);
            TestAssertions.Throws<ArgumentOutOfRangeException>(() => disk.Read(1));
            TestAssertions.Throws<ArgumentException>(() => disk.Write(0, [1]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static void SpiDiskReadWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-csharp-spi-{Guid.NewGuid():N}.img");
        try
        {
            File.WriteAllBytes(path, new byte[DiskImage.SectorSize * 4]);
            using var disk = new DiskImage();
            disk.Open(path);
            var source = new byte[DiskImage.SectorSize];
            source[0] = 0x12;
            source[DiskImage.SectorSize - 1] = 0x34;
            disk.Write(1, source);

            var memory = new Memory();
            memory.AttachDisk(disk);
            var control = DeviceBus.IoBase + (uint)MmioRegister.SpiControl;
            var data = DeviceBus.IoBase + (uint)MmioRegister.SpiData;
            memory.Write32(control, SpiDevice.Card0);
            TestFixtures.SendCommand(memory, data, 17, 1);
            TestAssertions.Equal(0x00u, TestFixtures.Transfer(memory, data, 0xff));
            TestAssertions.Equal(0xfeu, TestFixtures.Transfer(memory, data, 0xff));
            TestAssertions.Equal(0x12u, TestFixtures.Transfer(memory, data, 0xff));
            for (var index = 1; index < DiskImage.SectorSize - 1; index++) _ = TestFixtures.Transfer(memory, data, 0xff);
            TestAssertions.Equal(0x34u, TestFixtures.Transfer(memory, data, 0xff));
            TestFixtures.Transfer(memory, data, 0xff);
            TestFixtures.Transfer(memory, data, 0xff);

            TestFixtures.SendCommand(memory, data, 24, 2);
            TestAssertions.Equal(0x00u, TestFixtures.Transfer(memory, data, 0xff));
            TestFixtures.Transfer(memory, data, 0xfe);
            var replacement = new byte[DiskImage.SectorSize];
            replacement[0] = 0xa5;
            foreach (var value in replacement) TestFixtures.Transfer(memory, data, value);
            TestFixtures.Transfer(memory, data, 0xff);
            TestAssertions.Equal(0x05u, TestFixtures.Transfer(memory, data, 0xff));
            TestAssertions.Equal(0xa5, disk.Read(2)[0]);
        }
        finally { File.Delete(path); }
    }
}
