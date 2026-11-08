using Page = ProjectOberon.Emulator.OberonDirectoryPage;

namespace ProjectOberon.Emulator;

public sealed partial class OberonFileDirectory
{
    private Page ReadPage(int address)
    {
        return OberonDirectoryPageCodec.Read(_kernel.GetSector(address), address);
    }

    private void WritePage(int address, Page page)
    {
        _kernel.PutSector(address, OberonDirectoryPageCodec.Write(page));
    }

    private static int LowerBound(Page page, string name)
    {
        var low = 0;
        var high = page.Entries.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (string.CompareOrdinal(page.Entries[middle].Name, name) < 0) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
