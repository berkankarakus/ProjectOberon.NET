namespace ProjectOberon.Emulator;

public sealed class OberonKernel
{
    public const int SectorLength = 1024;
    public const int FileSystemOffset = 0x80000;
    public const int LogicalSectorUnit = 29;
    public const int MapSize = 0x10000;

    private readonly Memory _memory;
    private readonly DiskImage? _disk;
    private readonly bool[] _sectorMap = new bool[MapSize];
    private int _clock;

    public OberonKernel(Memory memory, DiskImage? disk = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _disk = disk;
        InitSectorMap();
    }

    public int AllocatedSectors { get; private set; }
    public int Clock => _clock;
    public int Time => checked((int)_memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));

    public void InitSectorMap()
    {
        Array.Clear(_sectorMap);
        for (var index = 0; index < 64; index++) _sectorMap[index] = true;
        AllocatedSectors = 0;
    }

    public void MarkSector(int sector)
    {
        var index = SectorIndex(sector);
        _sectorMap[index] = true;
        AllocatedSectors++;
    }

    public void FreeSector(int sector)
    {
        var index = SectorIndex(sector);
        _sectorMap[index] = false;
        AllocatedSectors--;
    }

    public int AllocSector(int hint)
    {
        var index = SectorIndex(hint);
        do
        {
            index++;
            if (index == MapSize) index = 1;
        } while (_sectorMap[index]);

        _sectorMap[index] = true;
        AllocatedSectors++;
        return checked(index * LogicalSectorUnit);
    }

    public byte[] GetSector(int address)
    {
        var physical = PhysicalSector(address);
        var first = RequireDisk().Read(physical);
        var second = RequireDisk().Read(physical + 1);
        var result = new byte[SectorLength];
        first.CopyTo(result, 0);
        second.CopyTo(result, DiskImage.SectorSize);
        return result;
    }

    public void PutSector(int address, ReadOnlySpan<byte> data)
    {
        if (data.Length != SectorLength) throw new ArgumentException($"sector must contain {SectorLength} bytes", nameof(data));
        var physical = PhysicalSector(address);
        RequireDisk().Write(physical, data[..DiskImage.SectorSize]);
        RequireDisk().Write(physical + 1, data[DiskImage.SectorSize..]);
    }

    public void SetClock(int value) => _clock = value;

    public static uint PhysicalSector(int address)
    {
        var index = SectorIndex(address);
        return checked((uint)(FileSystemOffset + index * 2));
    }

    private DiskImage RequireDisk() => _disk ?? throw new InvalidOperationException("Kernel disk is not attached");

    private static int SectorIndex(int address)
    {
        if (address < 0 || address % LogicalSectorUnit != 0)
            throw new ArgumentOutOfRangeException(nameof(address), "Oberon disk addresses must be non-negative multiples of 29");
        var index = address / LogicalSectorUnit;
        if (index >= MapSize) throw new ArgumentOutOfRangeException(nameof(address));
        return index;
    }
}
