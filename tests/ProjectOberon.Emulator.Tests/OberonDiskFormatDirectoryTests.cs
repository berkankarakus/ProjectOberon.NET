using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static partial class OberonDiskFormatTests
{
    private static void RoundTripsDirectoryPage()
    {
        var expected = new OberonDirectoryPage
        {
            Children = [29, 58],
            Entries = [new OberonDirectoryRecord("Demo.Mod", 87, 58)]
        };

        var actual = OberonDirectoryPageCodec.Read(OberonDirectoryPageCodec.Write(expected), 29);

        TestAssertions.Equal(1, actual.Entries.Count);
        TestAssertions.Equal("Demo.Mod", actual.Entries[0].Name);
        TestAssertions.Equal(87, actual.Entries[0].Address);
        TestAssertions.Equal(2, actual.Children.Count);
        TestAssertions.Equal(58, actual.Children[1]);
    }

    private static void RejectsInvalidDirectoryPageShape()
    {
        TestAssertions.Throws<ArgumentNullException>(() => OberonDirectoryPageCodec.Write(null!));

        var tooManyEntries = new OberonDirectoryPage
        {
            Entries = Enumerable.Range(0, OberonDirectoryPageCodec.MaxEntries + 1)
                .Select(index => new OberonDirectoryRecord($"File{index}", 29, 0)).ToList(),
            Children = Enumerable.Repeat(0, OberonDirectoryPageCodec.MaxEntries + 2).ToList()
        };

        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Write(tooManyEntries));
        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Entries = [new OberonDirectoryRecord("Demo", 29, 0)],
            Children = [0]
        }));
        TestAssertions.Throws<ArgumentException>(() => OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Entries = [new OberonDirectoryRecord(new string('x', 32), 1, 2)],
            Children = [0, 2]
        }));
        TestAssertions.Throws<ArgumentException>(() => OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Entries = [new OberonDirectoryRecord("bad\0name", 1, 2)],
            Children = [0, 2]
        }));
        TestAssertions.Throws<ArgumentException>(() => OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Entries = [new OberonDirectoryRecord("café", 1, 2)],
            Children = [0, 2]
        }));

        var invalidCount = new byte[OberonDirectoryPageCodec.PageSize];
        BinaryPrimitives.WriteInt32LittleEndian(invalidCount, OberonDirectoryPageCodec.DirectoryMark);
        BinaryPrimitives.WriteInt32LittleEndian(invalidCount.AsSpan(4), OberonDirectoryPageCodec.MaxEntries + 1);
        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Read(invalidCount, 29));

        var invalidName = new byte[OberonDirectoryPageCodec.PageSize];
        BinaryPrimitives.WriteInt32LittleEndian(invalidName, OberonDirectoryPageCodec.DirectoryMark);
        BinaryPrimitives.WriteInt32LittleEndian(invalidName.AsSpan(4), 1);
        invalidName[OberonDirectoryPageCodec.HeaderSize] = 0xff;
        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Read(invalidName, 29));

        var unterminatedName = new byte[OberonDirectoryPageCodec.PageSize];
        BinaryPrimitives.WriteInt32LittleEndian(unterminatedName, OberonDirectoryPageCodec.DirectoryMark);
        BinaryPrimitives.WriteInt32LittleEndian(unterminatedName.AsSpan(4), 1);
        unterminatedName.AsSpan(OberonDirectoryPageCodec.HeaderSize, 32).Fill((byte)'x');
        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Read(unterminatedName, 29));

        var invalidAddress = OberonDirectoryPageCodec.Write(new OberonDirectoryPage
        {
            Entries = [new OberonDirectoryRecord("Demo", 29, 0)],
            Children = [0, 0]
        });
        BinaryPrimitives.WriteInt32LittleEndian(
            invalidAddress.AsSpan(OberonDirectoryPageCodec.HeaderSize + 32),
            31);
        TestAssertions.Throws<InvalidDataException>(() => OberonDirectoryPageCodec.Read(invalidAddress, 29));
    }
}
