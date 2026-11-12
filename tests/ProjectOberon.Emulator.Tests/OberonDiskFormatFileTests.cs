using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static partial class OberonDiskFormatTests
{
    private static void RoundTripsFileHeaderAndExtensions()
    {
        var sector = new byte[OberonDiskLayout.SectorLength];
        OberonFileHeaderCodec.Write(
            sector,
            "Demo.Mod",
            allocatedPages: 65,
            lastPageLength: 512,
            date: 123,
            extensions: [2001],
            pages: Enumerable.Range(1, 66).Select(index => index * OberonDiskLayout.LogicalSectorUnit).ToArray());

        var header = OberonFileHeaderCodec.Read(sector, 29);
        var extension = new byte[OberonDiskLayout.SectorLength];
        for (var index = 0; index < 2; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                extension.AsSpan(index * 4),
                (64 + index + 1) * OberonDiskLayout.LogicalSectorUnit);
        }

        var pages = OberonFileHeaderCodec.ReadPageAddresses(header, address =>
            address == 2001 ? extension : throw new InvalidOperationException("unexpected extension address"));

        TestAssertions.Equal("Demo.Mod", header.Name);
        TestAssertions.Equal(65, header.AllocatedPages);
        TestAssertions.Equal(512, header.LastPageLength);
        TestAssertions.Equal(123, header.Date);
        TestAssertions.Equal(66, pages.Length);
        TestAssertions.Equal(66 * OberonDiskLayout.LogicalSectorUnit, pages[^1]);
        TestAssertions.Equal(512 + 65 * OberonDiskLayout.SectorLength, header.TotalLength);
    }

    private static void RejectsInvalidFileHeader()
    {
        var sector = new byte[OberonDiskLayout.SectorLength];

        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.Read(sector, 29));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Read(sector[..100], 29));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Write(new byte[100], "Demo", 0, 0, 0, [], []));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Write(sector, new string('x', 32), 0, 0, 0, [], []));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Write(sector, "café", 0, 0, 0, [], []));
        TestAssertions.Throws<ArgumentNullException>(() => OberonFileHeaderCodec.Write(sector, null!, 0, 0, 0, [], []));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => OberonFileHeaderCodec.Write(
            sector,
            "Demo",
            -1,
            OberonFileHeader.HeaderSize,
            0,
            [],
            [29]
        ));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => OberonFileHeaderCodec.Write(
            sector,
            "Demo",
            0,
            0,
            0,
            [],
            [29]
        ));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Write(
            sector,
            "Demo",
            1,
            OberonFileHeader.HeaderSize,
            0,
            [],
            [29]
        ));
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.Write(
            sector,
            "Demo",
            0,
            OberonFileHeader.HeaderSize,
            0,
            [],
            [31]
        ));
        TestAssertions.Throws<ArgumentException>(() => OberonFileHeaderCodec.Write(
            sector,
            "Demo",
            0,
            OberonFileHeader.HeaderSize,
            0,
            Enumerable.Repeat(29, OberonFileHeaderCodec.ExtensionCount + 1).ToArray(),
            [29]
        ));

        var invalidName = new byte[OberonDiskLayout.SectorLength];
        BinaryPrimitives.WriteInt32LittleEndian(invalidName, OberonFileHeaderCodec.HeaderMark);
        invalidName[4] = 0xff;
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.Read(invalidName, 29));

        var unterminatedName = new byte[OberonDiskLayout.SectorLength];
        BinaryPrimitives.WriteInt32LittleEndian(unterminatedName, OberonFileHeaderCodec.HeaderMark);
        unterminatedName.AsSpan(4, 32).Fill((byte)'x');
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.Read(unterminatedName, 29));

        var invalidLength = new byte[OberonDiskLayout.SectorLength];
        BinaryPrimitives.WriteInt32LittleEndian(invalidLength, OberonFileHeaderCodec.HeaderMark);
        BinaryPrimitives.WriteInt32LittleEndian(invalidLength.AsSpan(36), 1);
        BinaryPrimitives.WriteInt32LittleEndian(invalidLength.AsSpan(40), 0);
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.Read(invalidLength, 29));
    }

    private static void RejectsMissingFilePages()
    {
        var header = new OberonFileHeader("Demo", 1, 1, 0, [0], [29, 0]);

        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.ReadPageAddresses(header, _ => []));
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.ReadPageAddresses(
            header with { AllocatedPages = OberonFileHeaderCodec.MaxAllocatedPages + 1 },
            _ => []));

        var extensionHeader = header with
        {
            AllocatedPages = OberonFileHeaderCodec.SectorCount,
            ExtensionAddresses = [2000]
        };
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.ReadPageAddresses(
            extensionHeader,
            _ => new byte[4]));

        var invalidPage = header with { SectorAddresses = [31] };
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.ReadPageAddresses(
            invalidPage,
            _ => new byte[OberonDiskLayout.SectorLength]));

        var invalidExtension = extensionHeader with { ExtensionAddresses = [-29] };
        TestAssertions.Throws<InvalidDataException>(() => OberonFileHeaderCodec.ReadPageAddresses(
            invalidExtension,
            _ => new byte[OberonDiskLayout.SectorLength]));
    }
}
