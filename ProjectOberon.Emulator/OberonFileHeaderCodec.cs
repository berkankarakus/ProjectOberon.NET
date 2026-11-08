using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonFileHeaderCodec
{
    public const int HeaderSize = 352;
    public const int ExtensionCount = 12;
    public const int SectorCount = 64;
    public const int HeaderMark = unchecked((int)0x9ba7_1d86);

    public static OberonFileHeader Read(ReadOnlySpan<byte> sector, int headerAddress)
    {
        if (sector.Length < OberonDiskLayout.SectorLength)
        {
            throw new ArgumentException("header sector is too small", nameof(sector));
        }

        if (ReadInt32(sector, 0) != HeaderMark)
        {
            throw new InvalidDataException($"invalid Oberon file header at sector {headerAddress:x}");
        }

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
        if (header.AllocatedPages < 0)
        {
            throw new InvalidDataException("invalid Oberon file length");
        }

        var pages = new int[header.AllocatedPages + 1];
        for (var index = 0; index < pages.Length; index++)
        {
            if (index < SectorCount)
            {
                pages[index] = header.SectorAddresses[index];
            }
            else
            {
                var extensionIndex = (index - SectorCount) / 256;
                var entry = (index - SectorCount) % 256;
                if (extensionIndex >= header.ExtensionAddresses.Length || header.ExtensionAddresses[extensionIndex] == 0)
                {
                    throw new InvalidDataException("Oberon file header contains a missing extension page");
                }

                var extension = readSector(header.ExtensionAddresses[extensionIndex]);
                pages[index] = ReadInt32(extension, entry * 4);
            }

            if (pages[index] == 0)
            {
                throw new InvalidDataException("Oberon file header contains a missing page address");
            }
        }

        return pages;
    }

    public static void Write(Span<byte> sector, string name, int allocatedPages, int lastPageLength, int date,
        ReadOnlySpan<int> extensions, ReadOnlySpan<int> pages)
    {
        if (sector.Length < OberonDiskLayout.SectorLength)
        {
            throw new ArgumentException("header sector is too small", nameof(sector));
        }

        if (name.Length >= 32)
        {
            throw new ArgumentException("Oberon file name is too long", nameof(name));
        }

        sector.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(sector, HeaderMark);
        Encoding.ASCII.GetBytes(name).CopyTo(sector[4..]);
        BinaryPrimitives.WriteInt32LittleEndian(sector[36..], allocatedPages);
        BinaryPrimitives.WriteInt32LittleEndian(sector[40..], lastPageLength);
        BinaryPrimitives.WriteInt32LittleEndian(sector[44..], date);
        for (var index = 0; index < Math.Min(ExtensionCount, extensions.Length); index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(sector[(48 + index * 4)..], extensions[index]);
        }

        for (var index = 0; index < Math.Min(SectorCount, pages.Length); index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(sector[(96 + index * 4)..], pages[index]);
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }

    private static int[] ReadIntArray(ReadOnlySpan<byte> data, int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadInt32(data, offset + index * 4);
        }

        return result;
    }

    private static string ReadName(ReadOnlySpan<byte> data, int offset)
    {
        var length = 0;
        while (length < 32 && data[offset + length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }
}
