namespace ProjectOberon.Emulator;

public static class OberonDiskLayout
{
    public const int SectorLength = 1024;
    public const int FileSystemOffset = 0x80000;
    public const int LogicalSectorUnit = 29;
    public const int DirectoryRootAddress = LogicalSectorUnit;

    public static uint PhysicalSector(int address)
    {
        if (address < 0 || address % LogicalSectorUnit != 0)
            throw new ArgumentOutOfRangeException(nameof(address), "Oberon disk addresses must be non-negative multiples of 29");
        return checked((uint)(FileSystemOffset + (address / LogicalSectorUnit) * 2));
    }

    public static byte[] ReadSector(DiskImage disk, int address)
    {
        ArgumentNullException.ThrowIfNull(disk);
        var physical = PhysicalSector(address);
        var result = new byte[SectorLength];
        disk.Read(physical, result.AsSpan(0, DiskImage.SectorSize));
        disk.Read(physical + 1, result.AsSpan(DiskImage.SectorSize));
        return result;
    }

    public static void WriteSector(DiskImage disk, int address, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(disk);
        if (data.Length != SectorLength)
        {
            throw new ArgumentException($"sector must contain {SectorLength} bytes", nameof(data));
        }
        var physical = PhysicalSector(address);
        disk.Write(physical, data[..DiskImage.SectorSize]);
        disk.Write(physical + 1, data[DiskImage.SectorSize..]);
    }
}
