using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

public sealed record OberonDirectoryEntry(string Name, int HeaderAddress);

public sealed record OberonFileHeader(
    string Name,
    int AllocatedPages,
    int LastPageLength,
    int Date,
    int[] ExtensionAddresses,
    int[] SectorAddresses)
{
    public const int HeaderSize = 352;
    public int TotalLength => checked(AllocatedPages * OberonFileSystem.SectorLength + LastPageLength);
    public int ContentLength => Math.Max(0, TotalLength - HeaderSize);
}

public sealed class OberonFileSystem
{
    public const int SectorLength = OberonKernel.SectorLength;
    public const int FileSystemOffset = OberonKernel.FileSystemOffset;
    public const int LogicalSectorUnit = OberonKernel.LogicalSectorUnit;
    public const int DirectoryRootAddress = 29;
    public const int DirectoryMark = unchecked((int)0x9b1e_a38d);
    public const int HeaderMark = unchecked((int)0x9ba7_1d86);
    private const int DirectoryHeaderSize = 64;
    private const int DirectoryEntrySize = 40;
    private const int DirectoryEntryCount = 24;

    private readonly DiskImage _disk;

    public OberonFileSystem(DiskImage disk) => _disk = disk ?? throw new ArgumentNullException(nameof(disk));

    public IEnumerable<OberonDirectoryEntry> Enumerate(string prefix = "")
    {
        var entries = new List<OberonDirectoryEntry>();
        EnumeratePage(DirectoryRootAddress, prefix, entries, new HashSet<int>());
        return entries;
    }

    public OberonFileHeader ReadHeader(int headerAddress)
    {
        var sector = ReadLogicalSector(headerAddress);
        if (ReadInt32(sector, 0) != HeaderMark) throw new InvalidDataException($"invalid Oberon file header at sector {headerAddress:x}");
        var name = ReadName(sector, 4);
        var allocatedPages = ReadInt32(sector, 36);
        var lastPageLength = ReadInt32(sector, 40);
        var date = ReadInt32(sector, 44);
        var extensions = ReadIntArray(sector, 48, 12);
        var sectors = ReadIntArray(sector, 96, 64);
        return new OberonFileHeader(name, allocatedPages, lastPageLength, date, extensions, sectors);
    }

    public byte[] ReadFile(OberonDirectoryEntry entry)
    {
        var header = ReadHeader(entry.HeaderAddress);
        var result = new byte[header.ContentLength];
        var headerSector = ReadLogicalSector(entry.HeaderAddress);
        var copied = Math.Min(result.Length, SectorLength - OberonFileHeader.HeaderSize);
        headerSector.AsSpan(OberonFileHeader.HeaderSize, copied).CopyTo(result);
        var destination = copied;
        var page = 1;
        while (destination < result.Length)
        {
            if (page >= header.SectorAddresses.Length || header.SectorAddresses[page] == 0)
                throw new InvalidDataException($"file {entry.Name} has a missing data sector");
            var data = ReadLogicalSector(header.SectorAddresses[page]);
            var count = Math.Min(SectorLength, result.Length - destination);
            data.AsSpan(0, count).CopyTo(result.AsSpan(destination));
            destination += count;
            page++;
        }
        return result;
    }

    public byte[] ReadLogicalSector(int address)
    {
        if (address < 0 || address % LogicalSectorUnit != 0)
            throw new ArgumentOutOfRangeException(nameof(address), "Oberon disk addresses must be non-negative multiples of 29");
        var physicalSector = OberonKernel.PhysicalSector(address);
        var first = _disk.Read(physicalSector);
        var second = _disk.Read(physicalSector + 1);
        var result = new byte[SectorLength];
        first.CopyTo(result, 0);
        second.CopyTo(result, DiskImage.SectorSize);
        return result;
    }

    public static uint PhysicalSector(int address) => OberonKernel.PhysicalSector(address);

    private void EnumeratePage(int address, string prefix, List<OberonDirectoryEntry> entries, HashSet<int> visited)
    {
        if (!visited.Add(address)) throw new InvalidDataException($"directory cycle at sector {address}");
        var sector = ReadLogicalSector(address);
        if (ReadInt32(sector, 0) != DirectoryMark) throw new InvalidDataException($"invalid Oberon directory page at sector {address:x}");
        var count = ReadInt32(sector, 4);
        if (count < 0 || count > DirectoryEntryCount) throw new InvalidDataException($"invalid directory entry count: {count}");
        var leftChild = ReadInt32(sector, 8);
        for (var index = 0; index < count; index++)
        {
            var child = index == 0 ? leftChild : ReadInt32(sector, DirectoryHeaderSize + (index - 1) * DirectoryEntrySize + 36);
            if (child != 0) EnumeratePage(child, prefix, entries, visited);
            var entryOffset = DirectoryHeaderSize + index * DirectoryEntrySize;
            var name = ReadName(sector, entryOffset);
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                entries.Add(new OberonDirectoryEntry(name, ReadInt32(sector, entryOffset + 32)));
        }
        if (count > 0)
        {
            var rightChild = ReadInt32(sector, DirectoryHeaderSize + (count - 1) * DirectoryEntrySize + 36);
            if (rightChild != 0) EnumeratePage(rightChild, prefix, entries, visited);
        }
    }

    private static int ReadInt32(byte[] data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

    private static int[] ReadIntArray(byte[] data, int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++) result[index] = ReadInt32(data, offset + index * 4);
        return result;
    }

    private static string ReadName(byte[] data, int offset)
    {
        var end = offset;
        while (end < offset + 32 && data[end] != 0) end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }
}
