using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonDiskLayout
{
    public const int SectorLength = 1024;
    public const int FileSystemOffset = 0x80000;
    public const int LogicalSectorUnit = 29;

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
        var first = disk.Read(physical);
        var second = disk.Read(physical + 1);
        var result = new byte[SectorLength];
        first.CopyTo(result, 0);
        second.CopyTo(result, DiskImage.SectorSize);
        return result;
    }

    public static void WriteSector(DiskImage disk, int address, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(disk);
        if (data.Length != SectorLength) throw new ArgumentException($"sector must contain {SectorLength} bytes", nameof(data));
        var physical = PhysicalSector(address);
        disk.Write(physical, data[..DiskImage.SectorSize]);
        disk.Write(physical + 1, data[DiskImage.SectorSize..]);
    }
}

public static class OberonFileHeaderCodec
{
    public const int HeaderSize = 352;
    public const int ExtensionCount = 12;
    public const int SectorCount = 64;
    public const int HeaderMark = unchecked((int)0x9ba7_1d86);

    public static OberonFileHeader Read(ReadOnlySpan<byte> sector, int headerAddress)
    {
        if (sector.Length < OberonDiskLayout.SectorLength) throw new ArgumentException("header sector is too small", nameof(sector));
        if (ReadInt32(sector, 0) != HeaderMark) throw new InvalidDataException($"invalid Oberon file header at sector {headerAddress:x}");
        return new OberonFileHeader(
            ReadName(sector, 4),
            ReadInt32(sector, 36),
            ReadInt32(sector, 40),
            ReadInt32(sector, 44),
            ReadIntArray(sector, 48, ExtensionCount),
            ReadIntArray(sector, 96, SectorCount));
    }

    public static int[] ReadPageAddresses(OberonFileHeader header, Func<int, byte[]> readSector)
    {
        ArgumentNullException.ThrowIfNull(readSector);
        if (header.AllocatedPages < 0) throw new InvalidDataException("invalid Oberon file length");
        var pages = new int[header.AllocatedPages + 1];
        for (var index = 0; index < pages.Length; index++)
        {
            if (index < SectorCount) pages[index] = header.SectorAddresses[index];
            else
            {
                var extensionIndex = (index - SectorCount) / 256;
                var entry = (index - SectorCount) % 256;
                if (extensionIndex >= header.ExtensionAddresses.Length || header.ExtensionAddresses[extensionIndex] == 0)
                    throw new InvalidDataException("Oberon file header contains a missing extension page");
                var extension = readSector(header.ExtensionAddresses[extensionIndex]);
                pages[index] = ReadInt32(extension, entry * 4);
            }
            if (pages[index] == 0) throw new InvalidDataException("Oberon file header contains a missing page address");
        }
        return pages;
    }

    public static void Write(Span<byte> sector, string name, int allocatedPages, int lastPageLength, int date,
        ReadOnlySpan<int> extensions, ReadOnlySpan<int> pages)
    {
        if (sector.Length < OberonDiskLayout.SectorLength) throw new ArgumentException("header sector is too small", nameof(sector));
        if (name.Length >= 32) throw new ArgumentException("Oberon file name is too long", nameof(name));
        sector.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(sector, HeaderMark);
        Encoding.ASCII.GetBytes(name).CopyTo(sector[4..]);
        BinaryPrimitives.WriteInt32LittleEndian(sector[36..], allocatedPages);
        BinaryPrimitives.WriteInt32LittleEndian(sector[40..], lastPageLength);
        BinaryPrimitives.WriteInt32LittleEndian(sector[44..], date);
        for (var index = 0; index < Math.Min(ExtensionCount, extensions.Length); index++)
            BinaryPrimitives.WriteInt32LittleEndian(sector[(48 + index * 4)..], extensions[index]);
        for (var index = 0; index < Math.Min(SectorCount, pages.Length); index++)
            BinaryPrimitives.WriteInt32LittleEndian(sector[(96 + index * 4)..], pages[index]);
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);

    private static int[] ReadIntArray(ReadOnlySpan<byte> data, int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++) result[index] = ReadInt32(data, offset + index * 4);
        return result;
    }

    private static string ReadName(ReadOnlySpan<byte> data, int offset)
    {
        var length = 0;
        while (length < 32 && data[offset + length] != 0) length++;
        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }
}

public sealed class OberonDirectoryPage
{
    public List<OberonDirectoryRecord> Entries { get; set; } = [];
    public List<int> Children { get; set; } = [0];
}

public readonly record struct OberonDirectoryRecord(string Name, int Address, int Child);

public static class OberonDirectoryPageCodec
{
    public const int PageSize = 1024;
    public const int HeaderSize = 64;
    public const int EntrySize = 40;
    public const int MaxEntries = 24;
    public const int DirectoryMark = unchecked((int)0x9b1e_a38d);

    public static OberonDirectoryPage Read(ReadOnlySpan<byte> data, int address)
    {
        if (data.Length < PageSize) throw new ArgumentException("directory page is too small", nameof(data));
        if (BinaryPrimitives.ReadInt32LittleEndian(data) != DirectoryMark)
            throw new InvalidDataException($"invalid Oberon directory page at sector {address:x}");
        var count = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        if (count < 0 || count > MaxEntries) throw new InvalidDataException($"invalid directory entry count: {count}");
        var page = new OberonDirectoryPage { Children = [BinaryPrimitives.ReadInt32LittleEndian(data[8..])] };
        for (var index = 0; index < count; index++)
        {
            var offset = HeaderSize + index * EntrySize;
            var entry = new OberonDirectoryRecord(
                ReadName(data, offset),
                BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 32)..]),
                BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 36)..]));
            page.Entries.Add(entry);
            page.Children.Add(entry.Child);
        }
        return page;
    }

    public static byte[] Write(OberonDirectoryPage page)
    {
        if (page.Entries.Count > MaxEntries || page.Children.Count != page.Entries.Count + 1)
            throw new InvalidDataException("invalid Oberon directory page shape");
        var data = new byte[PageSize];
        BinaryPrimitives.WriteInt32LittleEndian(data, DirectoryMark);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), page.Entries.Count);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), page.Children[0]);
        for (var index = 0; index < page.Entries.Count; index++)
        {
            var offset = HeaderSize + index * EntrySize;
            var entry = page.Entries[index];
            Encoding.ASCII.GetBytes(entry.Name).CopyTo(data, offset);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 32), entry.Address);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 36), page.Children[index + 1]);
        }
        return data;
    }

    private static string ReadName(ReadOnlySpan<byte> data, int offset)
    {
        var length = 0;
        while (length < 32 && data[offset + length] != 0) length++;
        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }
}
