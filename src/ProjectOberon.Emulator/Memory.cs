using System.Buffers.Binary;
using System.Globalization;

namespace ProjectOberon.Emulator;

public interface IMemoryBus
{
    byte Read8(uint address);
    uint Read32(uint address);
    uint ReadInstruction(uint address);
    void Write8(uint address, byte value);
    void Write32(uint address, uint value);
}

public sealed class Memory : IMemoryBus
{
    public const uint AddressMask = MemoryMap.AddressMask;
    public const uint RomBase = MemoryMap.RomBase;
    public const int RomWords = MemoryMap.RomWords;

    private readonly byte[] _ram;
    private readonly uint[] _rom = new uint[MemoryMap.RomWords];
    private readonly DeviceBus _devices = new();
    private ulong _read32Count;
    private ulong _write32Count;
    private ulong _romRead32Count;
    private ulong _mmioRead32Count;
    private ulong _romWrite32Count;
    private ulong _mmioWrite32Count;

    public Memory(int ramBytes = 1 << 20)
    {
        if (ramBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ramBytes));
        }

        _ram = new byte[ramBytes];
    }

    public int RamSize => _ram.Length;
    public DeviceBus Devices => _devices;
    public bool CollectStatistics { get; set; }

    public MemoryStatistics GetStatistics() => new(
        _read32Count,
        _write32Count,
        _romRead32Count,
        _mmioRead32Count,
        _romWrite32Count,
        _mmioWrite32Count);

    public void ResetStatistics()
    {
        _read32Count = 0;
        _write32Count = 0;
        _romRead32Count = 0;
        _mmioRead32Count = 0;
        _romWrite32Count = 0;
        _mmioWrite32Count = 0;
    }

    public byte Read8(uint address)
    {
        address &= MemoryMap.AddressMask;
        if (_devices.Handles(address))
            return (byte)(_devices.Read(address & ~3u) >> (int)((address & 3u) * 8));
        if (IsRom(address))
        {
            var word = _rom[(int)((address >> 2) & (MemoryMap.RomWords - 1))];
            return (byte)(word >> (int)((address & 3u) * 8));
        }

        return _ram[RamIndex(address)];
    }

    public uint Read32(uint address)
    {
        if (CollectStatistics)
        {
            _read32Count++;
        }

        address &= MemoryMap.AddressMask;
        if (_devices.Handles(address))
        {
            if (CollectStatistics)
            {
                _mmioRead32Count++;
            }

            return _devices.Read(address & ~3u);
        }

        if (IsRom(address))
        {
            if (CollectStatistics)
            {
                _romRead32Count++;
            }

            return _rom[(int)((address >> 2) & (MemoryMap.RomWords - 1))];
        }

        var ramIndex = RamIndex(address);
        if (ramIndex <= _ram.Length - sizeof(uint))
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(
                _ram.AsSpan(ramIndex, sizeof(uint)));
        }

        return (uint)(Read8(address)
            | ((uint)Read8(address + 1) << 8)
            | ((uint)Read8(address + 2) << 16)
            | ((uint)Read8(address + 3) << 24));
    }

    public uint ReadInstruction(uint address)
    {
        address &= MemoryMap.AddressMask;
        return IsRom(address) ? _rom[(int)((address >> 2) & (MemoryMap.RomWords - 1))] : Read32(address);
    }

    public void Write8(uint address, byte value)
    {
        address &= MemoryMap.AddressMask;
        if (_devices.Handles(address))
        {
            var aligned = address & ~3u;
            var shift = (int)((address & 3u) * 8);
            var old = _devices.Read(aligned);
            _devices.Write(aligned, (old & ~(0xffu << shift)) | ((uint)value << shift));
            return;
        }
        if (!IsRom(address))
        {
            _ram[RamIndex(address)] = value;
        }
    }

    public void Write32(uint address, uint value)
    {
        if (CollectStatistics)
        {
            _write32Count++;
        }

        address &= MemoryMap.AddressMask;
        if (_devices.Handles(address))
        {
            if (CollectStatistics)
            {
                _mmioWrite32Count++;
            }

            _devices.Write(address & ~3u, value);
            return;
        }

        if (IsRom(address))
        {
            if (CollectStatistics)
            {
                _romWrite32Count++;
            }

            return;
        }

        var ramIndex = RamIndex(address);
        if (ramIndex <= _ram.Length - sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                _ram.AsSpan(ramIndex, sizeof(uint)),
                value
            );
            return;
        }

        Write8(address, (byte)value);
        Write8(address + 1, (byte)(value >> 8));
        Write8(address + 2, (byte)(value >> 16));
        Write8(address + 3, (byte)(value >> 24));
    }

    public void Clear()
    {
        Array.Clear(_ram);
        Array.Clear(_rom);
        _devices.Reset();
        ResetStatistics();
    }

    public void LoadRomWords(ReadOnlySpan<uint> words)
    {
        if (words.Length != MemoryMap.RomWords)
        {
            throw new ArgumentException(
                $"ROM must contain exactly {MemoryMap.RomWords} words",
                nameof(words));
        }

        words.CopyTo(_rom);
    }

    public void LoadRomFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var words = new uint[MemoryMap.RomWords];
        var index = 0;
        var lineNumber = 0;
        foreach (var originalLine in File.ReadLines(path))
        {
            lineNumber++;
            if (index >= MemoryMap.RomWords)
            {
                throw new InvalidDataException($"ROM file contains more than {MemoryMap.RomWords} words at line {lineNumber}: {path}");
            }
            var line = originalLine;
            var comment = line.IndexOfAny(['#', ';']);
            if (comment >= 0)
            {
                line = line[..comment];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            if (!uint.TryParse(line.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var word))
                throw new InvalidDataException($"Invalid ROM word at line {lineNumber}: {path}");
            words[index++] = word;
        }

        words.CopyTo(_rom, 0);
    }

    public void AttachDisk(DiskImage disk) => _devices.AttachDisk(disk);
    public void DetachDisk() => _devices.DetachDisk();
    public bool HasDisk => _devices.HasDisk;
    public void AdvanceTime() => _devices.AdvanceTime();
    public void EnqueueUartRx(byte value) => _devices.EnqueueUartRx(value);
    public void EnqueueKeyboard(byte value) => _devices.EnqueueKeyboard(value);
    public void SetMouse(ushort x, ushort y, byte buttons) => _devices.SetMouse(x, y, buttons);
    public string TakeUartOutput() => _devices.TakeUartOutput();

    private static bool IsRom(uint maskedAddress) => (maskedAddress >> 14) == 0x3ffu;
    private int RamIndex(uint maskedAddress) => (int)(maskedAddress % (uint)_ram.Length);
}
