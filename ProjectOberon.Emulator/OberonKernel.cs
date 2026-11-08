namespace ProjectOberon.Emulator;

public sealed class OberonKernel
{
    public const int SectorLength = OberonDiskLayout.SectorLength;
    public const int FileSystemOffset = OberonDiskLayout.FileSystemOffset;
    public const int LogicalSectorUnit = OberonDiskLayout.LogicalSectorUnit;
    public const int MapSize = 0x10000;

    private readonly Memory _memory;
    private readonly DiskImage? _disk;
    private readonly bool[] _sectorMap = new bool[MapSize];
    private readonly uint _heapLimit;
    private uint _heapNext;
    private int _clock;

    public OberonKernel(Memory memory, DiskImage? disk = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _disk = disk;
        _heapNext = 0x10000;
        _heapLimit = checked((uint)Math.Max((int)_heapNext, memory.RamSize - 0x8000));
        InitSectorMap();
    }

    public int AllocatedSectors { get; private set; }
    public int AllocatedBytes { get; private set; }
    public int Clock => _clock;
    public int Time => checked((int)_memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));

    public void InitSectorMap()
    {
        Array.Clear(_sectorMap);
        for (var index = 0; index < 64; index++)
        {
            _sectorMap[index] = true;
        }
        AllocatedSectors = 0;
    }

    public void MarkSector(int sector)
    {
        var index = SectorIndex(sector);
        _sectorMap[index] = true;
        AllocatedSectors++;
    }

    public void ReserveSector(int sector)
    {
        var index = SectorIndex(sector);
        if (_sectorMap[index])
        {
            return;
        }
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
        var limit = AvailableMapSize();
        var index = SectorIndex(hint);
        if (index >= limit)
        {
            index = 0;
        }

        for (var scanned = 0; scanned < limit - 1; scanned++)
        {
            index++;
            if (index >= limit) index = 1;
            if (_sectorMap[index]) continue;

            _sectorMap[index] = true;
            AllocatedSectors++;
            return checked(index * LogicalSectorUnit);
        }

        throw new InvalidOperationException("Oberon disk has no free logical sectors");
    }

    public uint New(uint tagAddress)
    {
        var size = checked((int)_memory.Read32(tagAddress));
        if (size < 8 || (size & 3) != 0)
        {
            throw new InvalidDataException("Oberon heap object size must be a multiple of four and include its header");
        }

        var end = checked(_heapNext + (uint)size);
        if (end > _heapLimit)
        {
            throw new OutOfMemoryException("Oberon heap is full");
        }

        var block = _heapNext;
        _heapNext = end;
        _memory.Write32(block, tagAddress);
        _memory.Write32(block + 4, 0);
        for (var address = block + 8; address < end; address += 4)
        {
            _memory.Write32(address, 0);
        }

        AllocatedBytes = checked(AllocatedBytes + size);
        return block + 8;
    }

    public byte[] GetSector(int address)
    {
        return OberonDiskLayout.ReadSector(RequireDisk(), address);
    }

    public void PutSector(int address, ReadOnlySpan<byte> data)
    {
        OberonDiskLayout.WriteSector(RequireDisk(), address, data);
    }

    public void SetClock(int value)
    {
        _clock = value;
    }

    public static uint PhysicalSector(int address)
    {
        return OberonDiskLayout.PhysicalSector(address);
    }

    private DiskImage RequireDisk()
    {
        return _disk ?? throw new InvalidOperationException("Kernel disk is not attached");
    }

    private static int SectorIndex(int address)
    {
        if (address < 0 || address % LogicalSectorUnit != 0)
            throw new ArgumentOutOfRangeException(nameof(address), "Oberon disk addresses must be non-negative multiples of 29");
        var index = address / LogicalSectorUnit;
        if (index >= MapSize)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        return index;
    }

    private int AvailableMapSize()
    {
        if (_disk is null)
        {
            return MapSize;
        }

        if (_disk.SectorCount <= FileSystemOffset)
        {
            throw new InvalidOperationException("Oberon disk image has no filesystem area");
        }

        var available = (_disk.SectorCount - (uint)FileSystemOffset) / 2;
        return checked((int)Math.Min((uint)MapSize, available));
    }
}
