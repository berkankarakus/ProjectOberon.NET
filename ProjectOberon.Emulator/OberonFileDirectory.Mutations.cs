using Entry = ProjectOberon.Emulator.OberonDirectoryRecord;
using Page = ProjectOberon.Emulator.OberonDirectoryPage;

namespace ProjectOberon.Emulator;

public sealed partial class OberonFileDirectory
{
    public void Insert(string name, int fileAddress)
    {
        OberonFileNameRules.Validate(name);
        if (fileAddress <= 0 || fileAddress % OberonKernel.LogicalSectorUnit != 0)
            throw new ArgumentOutOfRangeException(nameof(fileAddress));

        var promotion = InsertPage(RootAddress, new Entry(name, fileAddress, 0));
        if (promotion is null)
        {
            return;
        }
        var promoted = promotion.Value;

        var leftAddress = _kernel.AllocSector(RootAddress);
        var left = ReadPage(RootAddress);
        WritePage(leftAddress, left);
        var root = new Page
        {
            Children = [leftAddress, promoted.RightChild],
            Entries = [new Entry(promoted.Name, promoted.Address, 0)]
        };
        WritePage(RootAddress, root);
    }

    public bool Delete(string name, out int fileAddress)
    {
        OberonFileNameRules.Validate(name);
        var result = DeletePage(RootAddress, name);
        fileAddress = result.Address;
        if (!result.Found)
        {
            return false;
        }

        var root = ReadPage(RootAddress);
        if (root.Entries.Count == 0 && root.Children[0] != 0)
        {
            var child = ReadPage(root.Children[0]);
            WritePage(RootAddress, child);
        }
        return true;
    }

    public bool Rename(string oldName, string newName)
    {
        OberonFileNameRules.Validate(oldName);
        OberonFileNameRules.Validate(newName);
        var address = Search(oldName);
        if (address == 0)
        {
            return false;
        }
        Delete(oldName, out _);
        Insert(newName, address);
        return true;
    }

    private Promotion? InsertPage(int address, Entry entry)
    {
        var page = ReadPage(address);
        var index = LowerBound(page, entry.Name);
        if (index < page.Entries.Count && page.Entries[index].Name == entry.Name)
        {
            page.Entries[index] = entry with { Child = page.Entries[index].Child };
            WritePage(address, page);
            return null;
        }

        if (page.Children[index] != 0)
        {
            var promotion = InsertPage(page.Children[index], entry);
            if (promotion is null)
            {
                return null;
            }
            var promoted = promotion.Value;
            page.Entries.Insert(index, new Entry(promoted.Name, promoted.Address, promoted.RightChild));
            page.Children.Insert(index + 1, promoted.RightChild);
        }
        else
        {
            page.Entries.Insert(index, entry);
            page.Children.Insert(index + 1, 0);
        }

        if (page.Entries.Count <= MaxEntries)
        {
            WritePage(address, page);
            return null;
        }
        return SplitPage(address, page);
    }

    private Promotion SplitPage(int address, Page page)
    {
        var middle = page.Entries.Count / 2;
        var promoted = page.Entries[middle];
        var rightAddress = _kernel.AllocSector(address);
        var right = new Page
        {
            Entries = page.Entries[(middle + 1)..],
            Children = page.Children[(middle + 1)..]
        };
        page.Entries.RemoveRange(middle, page.Entries.Count - middle);
        page.Children.RemoveRange(middle + 1, page.Children.Count - (middle + 1));
        WritePage(address, page);
        WritePage(rightAddress, right);
        return new Promotion(promoted.Name, promoted.Address, rightAddress);
    }

    private DeleteResult DeletePage(int address, string name)
    {
        var page = ReadPage(address);
        var index = LowerBound(page, name);
        if (index < page.Entries.Count && page.Entries[index].Name == name)
        {
            var removed = page.Entries[index].Address;
            if (page.Children[index] == 0)
            {
                page.Entries.RemoveAt(index);
                page.Children.RemoveAt(index + 1);
                WritePage(address, page);
                return new DeleteResult(true, removed, page.Entries.Count < MinEntries);
            }

            var predecessor = Rightmost(page.Children[index]);
            page.Entries[index] = predecessor.Entry with { Child = page.Entries[index].Child };
            var result = DeletePage(page.Children[index], predecessor.Entry.Name);
            if (result.Underflow) Rebalance(page, index);
            WritePage(address, page);
            return new DeleteResult(true, removed, page.Entries.Count < MinEntries);
        }

        var child = page.Children[index];
        if (child == 0)
        {
            return new DeleteResult(false, 0, false);
        }
        var deleted = DeletePage(child, name);
        if (deleted.Underflow) Rebalance(page, index);
        WritePage(address, page);
        return new DeleteResult(deleted.Found, deleted.Address, page.Entries.Count < MinEntries);
    }

    private void Rebalance(Page page, int childIndex)
    {
        var childAddress = page.Children[childIndex];
        var child = ReadPage(childAddress);
        if (childIndex < page.Entries.Count)
        {
            var rightAddress = page.Children[childIndex + 1];
            var right = ReadPage(rightAddress);
            if (right.Entries.Count > MinEntries)
            {
                child.Entries.Add(page.Entries[childIndex]);
                child.Children.Add(right.Children[0]);
                page.Entries[childIndex] = right.Entries[0];
                right.Entries.RemoveAt(0);
                right.Children.RemoveAt(0);
                WritePage(childAddress, child);
                WritePage(rightAddress, right);
                return;
            }

            child.Entries.Add(page.Entries[childIndex]);
            child.Entries.AddRange(right.Entries);
            child.Children.AddRange(right.Children);
            page.Entries.RemoveAt(childIndex);
            page.Children.RemoveAt(childIndex + 1);
            WritePage(childAddress, child);
            return;
        }

        var leftAddress = page.Children[childIndex - 1];
        var left = ReadPage(leftAddress);
        if (left.Entries.Count > MinEntries)
        {
            child.Entries.Insert(0, page.Entries[childIndex - 1]);
            child.Children.Insert(0, left.Children[^1]);
            page.Entries[childIndex - 1] = left.Entries[^1];
            left.Entries.RemoveAt(left.Entries.Count - 1);
            left.Children.RemoveAt(left.Children.Count - 1);
            WritePage(leftAddress, left);
            WritePage(childAddress, child);
            return;
        }

        left.Entries.Add(page.Entries[childIndex - 1]);
        left.Entries.AddRange(child.Entries);
        left.Children.AddRange(child.Children);
        page.Entries.RemoveAt(childIndex - 1);
        page.Children.RemoveAt(childIndex);
        WritePage(leftAddress, left);
    }

    private EntryWithAddress Rightmost(int address)
    {
        var page = ReadPage(address);
        while (page.Children[^1] != 0) page = ReadPage(page.Children[^1]);
        return new EntryWithAddress(page.Entries[^1], address);
    }

    private readonly record struct Promotion(string Name, int Address, int RightChild);
    private readonly record struct EntryWithAddress(Entry Entry, int Address);
    private readonly record struct DeleteResult(bool Found, int Address, bool Underflow);
}
