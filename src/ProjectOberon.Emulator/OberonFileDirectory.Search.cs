using Page = ProjectOberon.Emulator.OberonDirectoryPage;

namespace ProjectOberon.Emulator;

public sealed partial class OberonFileDirectory
{
    public int Search(string name)
    {
        OberonFileNameRules.Validate(name);
        var address = RootAddress;
        while (address != 0)
        {
            var page = ReadPage(address);
            var index = LowerBound(page, name);
            if (index < page.Entries.Count && page.Entries[index].Name == name)
            {
                return page.Entries[index].Address;
            }
            address = page.Children[index];
        }
        return 0;
    }

    public IEnumerable<OberonDirectoryEntry> Enumerate(string prefix = "")
    {
        var result = new List<OberonDirectoryEntry>();
        EnumeratePage(RootAddress, prefix, result, new HashSet<int>());
        return result;
    }

    private void EnumeratePage(int address, string prefix, List<OberonDirectoryEntry> result, HashSet<int> visited)
    {
        if (address == 0 || !visited.Add(address))
        {
            return;
        }
        var page = ReadPage(address);
        for (var index = 0; index < page.Entries.Count; index++)
        {
            EnumeratePage(page.Children[index], prefix, result, visited);
            if (page.Entries[index].Name.StartsWith(prefix, StringComparison.Ordinal))
                result.Add(new OberonDirectoryEntry(page.Entries[index].Name, page.Entries[index].Address));
        }
        EnumeratePage(page.Children[^1], prefix, result, visited);
    }

    private void TraverseAndReserve(int address, List<int> files, HashSet<int> visited)
    {
        if (address == 0 || !visited.Add(address))
        {
            return;
        }
        var page = ReadPage(address);
        _kernel.ReserveSector(address);
        foreach (var entry in page.Entries)
        {
            files.Add(entry.Address);
        }
        TraverseAndReserve(page.Children[0], files, visited);
        foreach (var entry in page.Entries)
        {
            TraverseAndReserve(entry.Child, files, visited);
        }
    }

    private void ReserveFile(int headerAddress)
    {
        _kernel.ReserveSector(headerAddress);
        var header = _kernel.GetSector(headerAddress);
        var parsedHeader = OberonFileHeaderCodec.Read(header, headerAddress);
        foreach (var page in OberonFileHeaderCodec.ReadPageAddresses(parsedHeader, _kernel.GetSector))
            _kernel.ReserveSector(page);
        var extensionCount = (parsedHeader.AllocatedPages + (256 - 64)) / 256;
        for (var extensionIndex = 0; extensionIndex < extensionCount; extensionIndex++)
        {
            var extensionAddress = parsedHeader.ExtensionAddresses[extensionIndex];
            _kernel.ReserveSector(extensionAddress);
        }
    }
}
