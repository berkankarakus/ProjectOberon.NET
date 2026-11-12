using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonFileHeaderCodec
{
    public const int HeaderSize = 352;
    public const int ExtensionCount = 12;
    public const int SectorCount = 64;
    public const int MaxAllocatedPages = SectorCount + ExtensionCount * 256 - 1;
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
        if (header.AllocatedPages < 0 || header.AllocatedPages > MaxAllocatedPages)
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
                if (extension.Length < OberonDiskLayout.SectorLength)
                {
                    throw new InvalidDataException("Oberon file header extension page is too small");
                }

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

        ArgumentNullException.ThrowIfNull(name);
        if (allocatedPages < 0 || allocatedPages > MaxAllocatedPages)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedPages));
        }

        var minimumLastPageLength = allocatedPages == 0
            ? HeaderSize
            : 1;
        if (lastPageLength < minimumLastPageLength || lastPageLength > OberonDiskLayout.SectorLength)
        {
            throw new ArgumentOutOfRangeException(nameof(lastPageLength));
        }

        if (pages.Length < allocatedPages + 1)
        {
            throw new ArgumentException("page list is shorter than the allocated file", nameof(pages));
        }

        if (name.Length >= 32 || name.Any(character => character > 0x7f || character == '\0'))
        {
            throw new ArgumentException(
                "Oberon file name must be 31 ASCII characters or fewer",
                nameof(name));
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
            if (data[offset + length] > 0x7f)
            {
                throw new InvalidDataException("Oberon file names must contain ASCII characters");
            }

            length++;
        }

        if (length == 32)
        {
            throw new InvalidDataException("Oberon file name is not NUL-terminated");
        }

        return Encoding.ASCII.GetString(data.Slice(offset, length));
    }
}
