using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

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
        if (data.Length < PageSize)
        {
            throw new ArgumentException("directory page is too small", nameof(data));
        }

        if (BinaryPrimitives.ReadInt32LittleEndian(data) != DirectoryMark)
        {
            throw new InvalidDataException($"invalid Oberon directory page at sector {address:x}");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        if (count < 0 || count > MaxEntries)
        {
            throw new InvalidDataException($"invalid directory entry count: {count}");
        }

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
        {
            throw new InvalidDataException("invalid Oberon directory page shape");
        }

        var data = new byte[PageSize];
        BinaryPrimitives.WriteInt32LittleEndian(data, DirectoryMark);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), page.Entries.Count);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), page.Children[0]);
        for (var index = 0; index < page.Entries.Count; index++)
        {
            var offset = HeaderSize + index * EntrySize;
            var entry = page.Entries[index];
            EncodeName(entry.Name).CopyTo(data, offset);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 32), entry.Address);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 36), page.Children[index + 1]);
        }

        return data;
    }

    private static string ReadName(ReadOnlySpan<byte> data, int offset)
    {
        var length = 0;
        while (length < 32 && data[offset + length] != 0)
        {
            if (data[offset + length] > 0x7f)
            {
                throw new InvalidDataException("directory entry names must contain ASCII characters");
            }

            length++;
        }

        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }

    private static byte[] EncodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > 31 || name.Any(character => character > 0x7f || character == '\0'))
        {
            throw new ArgumentException(
                "directory entry name must be 31 ASCII characters or fewer",
                nameof(name));
        }

        return Encoding.ASCII.GetBytes(name);
    }
}
