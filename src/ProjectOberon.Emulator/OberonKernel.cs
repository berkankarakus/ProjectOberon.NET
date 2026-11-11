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
    private readonly List<FreeBlock> _freeBlocks = [];
    private readonly uint _heapOrigin;
    private readonly uint _heapLimit;
    private uint _heapNext;
    private int _clock;

    public OberonKernel(Memory memory, DiskImage? disk = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _disk = disk;
        _heapOrigin = 0x10000;
        _heapNext = _heapOrigin;
        _heapLimit = checked((uint)Math.Max((int)_heapOrigin, memory.RamSize - 0x8000));
        InitSectorMap();
    }

    public int AllocatedSectors { get; private set; }
    public int AllocatedBytes { get; private set; }
    public int Clock => _clock;
    public int Time => checked((int)_memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));

    public void Initialize()
    {
        _heapNext = _heapOrigin;
        AllocatedBytes = 0;
        _clock = 0;
        _freeBlocks.Clear();
        InitSectorMap();
    }

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
            if (_sectorMap[index])
            {
                continue;
            }

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

        if (TryTakeFreeBlock((uint)size, out var reusedBlock))
        {
            return InitializeObject(reusedBlock, tagAddress, size);
        }

        var end = checked(_heapNext + (uint)size);
        if (end > _heapLimit)
        {
            throw new OutOfMemoryException("Oberon heap is full");
        }

        var block = _heapNext;
        _heapNext = end;
        return InitializeObject(block, tagAddress, size);
    }

    public void Mark(uint rootListAddress)
    {
        var visited = new HashSet<uint>();
        var rootListCursor = rootListAddress;
        while (true)
        {
            var pointerAddress = _memory.Read32(rootListCursor);
            if (pointerAddress == 0)
            {
                return;
            }

            MarkObject(_memory.Read32(pointerAddress), visited);
            rootListCursor = checked(rootListCursor + 4);
        }
    }

    public void Scan()
    {
        var freeBlocks = new List<FreeBlock>();
        var liveBytes = 0;
        var address = _heapOrigin;
        while (address < _heapNext)
        {
            var mark = unchecked((int)_memory.Read32(address + 4));
            var size = mark == -1
                ? _memory.Read32(address)
                : ObjectSize(_memory.Read32(address));
            if (size < 8 || (size & 3) != 0 || size > _heapNext - address)
            {
                throw new InvalidDataException("invalid Oberon heap block");
            }

            if (mark == -1 || mark == 0)
            {
                freeBlocks.Add(new FreeBlock(address, size));
            }
            else
            {
                _memory.Write32(address + 4, 0);
                liveBytes = checked(liveBytes + (int)size);
            }

            address += size;
        }

        _freeBlocks.Clear();
        foreach (var block in Coalesce(freeBlocks))
        {
            _memory.Write32(block.Address, block.Size);
            _memory.Write32(block.Address + 4, unchecked((uint)-1));
            _freeBlocks.Add(block);
        }

        AllocatedBytes = liveBytes;
    }

    private uint InitializeObject(uint block, uint tagAddress, int size)
    {
        var end = checked(block + (uint)size);
        _memory.Write32(block, tagAddress);
        _memory.Write32(block + 4, 0);
        for (var address = block + 8; address < end; address += 4)
        {
            _memory.Write32(address, 0);
        }

        AllocatedBytes = checked(AllocatedBytes + size);
        return block + 8;
    }

    private void MarkObject(uint objectAddress, HashSet<uint> visited)
    {
        if (objectAddress < _heapOrigin + 8 || objectAddress >= _heapNext
            || (objectAddress & 3) != 0)
        {
            return;
        }

        var blockAddress = objectAddress - 8;
        if (!visited.Add(blockAddress) || _memory.Read32(blockAddress + 4) != 0)
        {
            return;
        }

        _memory.Write32(blockAddress + 4, 1);
        var tagAddress = _memory.Read32(blockAddress);
        var size = ObjectSize(tagAddress);
        var offsetAddress = checked(tagAddress + 16);
        while (true)
        {
            var offset = _memory.Read32(offsetAddress);
            if (offset == unchecked((uint)-1))
            {
                return;
            }

            if (offset >= size - 8 || (offset & 3) != 0)
            {
                throw new InvalidDataException("invalid Oberon pointer offset");
            }

            MarkObject(_memory.Read32(objectAddress + offset), visited);
            offsetAddress = checked(offsetAddress + 4);
        }
    }

    private uint ObjectSize(uint tagAddress)
    {
        var size = _memory.Read32(tagAddress);
        if (size < 8 || (size & 3) != 0)
        {
            throw new InvalidDataException("invalid Oberon object tag");
        }

        return size;
    }

    private bool TryTakeFreeBlock(uint size, out uint blockAddress)
    {
        for (var index = 0; index < _freeBlocks.Count; index++)
        {
            var block = _freeBlocks[index];
            if (block.Size < size)
            {
                continue;
            }

            var remainder = block.Size - size;
            if (remainder != 0 && remainder < 8)
            {
                continue;
            }

            _freeBlocks.RemoveAt(index);
            if (remainder >= 8)
            {
                var remainderAddress = block.Address + size;
                _memory.Write32(remainderAddress, remainder);
                _memory.Write32(remainderAddress + 4, unchecked((uint)-1));
                _freeBlocks.Add(new FreeBlock(remainderAddress, remainder));
            }

            blockAddress = block.Address;
            return true;
        }

        blockAddress = 0;
        return false;
    }

    private static IEnumerable<FreeBlock> Coalesce(IEnumerable<FreeBlock> blocks)
    {
        FreeBlock? pending = null;
        foreach (var block in blocks.OrderBy(block => block.Address))
        {
            if (pending is not FreeBlock current)
            {
                pending = block;
                continue;
            }

            if (current.Address + current.Size == block.Address)
            {
                pending = new FreeBlock(current.Address, current.Size + block.Size);
                continue;
            }

            yield return current;
            pending = block;
        }

        if (pending is FreeBlock last)
        {
            yield return last;
        }
    }

    private readonly record struct FreeBlock(uint Address, uint Size);

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

    internal void SyncModuleData(uint dataAddress)
    {
        _memory.Write32(dataAddress, checked((uint)AllocatedBytes));
        _memory.Write32(dataAddress + 4, checked((uint)AllocatedSectors));
        _memory.Write32(dataAddress + 8, _heapOrigin);
        _memory.Write32(dataAddress + 12, _heapLimit);
        _memory.Write32(dataAddress + 16, _heapOrigin);
        _memory.Write32(dataAddress + 20, 0x8000);
        _memory.Write32(dataAddress + 24, checked((uint)_memory.RamSize));
        _memory.Write32(dataAddress + 28, unchecked((uint)_clock));
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
