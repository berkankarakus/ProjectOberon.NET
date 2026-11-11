namespace ProjectOberon.Emulator;

public sealed class OberonFileSystem
{
    public const int SectorLength = OberonDiskLayout.SectorLength;
    public const int FileSystemOffset = OberonDiskLayout.FileSystemOffset;
    public const int LogicalSectorUnit = OberonDiskLayout.LogicalSectorUnit;
    public const int DirectoryRootAddress = OberonDiskLayout.DirectoryRootAddress;
    public const int DirectoryMark = OberonDirectoryPageCodec.DirectoryMark;
    public const int HeaderMark = OberonFileHeaderCodec.HeaderMark;

    private readonly DiskImage _disk;

    public OberonFileSystem(DiskImage disk)
    {
        _disk = disk ?? throw new ArgumentNullException(nameof(disk));
    }

    public IEnumerable<OberonDirectoryEntry> Enumerate(string prefix = "")
    {
        var entries = new List<OberonDirectoryEntry>();
        EnumeratePage(DirectoryRootAddress, prefix, entries, new HashSet<int>());
        return entries;
    }

    public OberonFileHeader ReadHeader(int headerAddress)
    {
        return OberonFileHeaderCodec.Read(ReadLogicalSector(headerAddress), headerAddress);
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
        return OberonDiskLayout.ReadSector(_disk, address);
    }

    public static uint PhysicalSector(int address) => OberonDiskLayout.PhysicalSector(address);

    private void EnumeratePage(int address, string prefix, List<OberonDirectoryEntry> entries, HashSet<int> visited)
    {
        if (!visited.Add(address))
        {
            throw new InvalidDataException($"directory cycle at sector {address}");
        }
        var page = OberonDirectoryPageCodec.Read(ReadLogicalSector(address), address);
        for (var index = 0; index < page.Entries.Count; index++)
        {
            var child = page.Children[index];
            if (child != 0)
            {
                EnumeratePage(child, prefix, entries, visited);
            }
            var entry = page.Entries[index];
            if (entry.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                entries.Add(new OberonDirectoryEntry(entry.Name, entry.Address));
            }
        }
        if (page.Children[^1] != 0)
        {
            EnumeratePage(page.Children[^1], prefix, entries, visited);
        }
    }
}
