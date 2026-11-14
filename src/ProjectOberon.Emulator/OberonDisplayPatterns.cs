using System.Buffers;

namespace ProjectOberon.Emulator;

public sealed partial class OberonDisplay
{
    public void CopyPattern(int color, uint patternAddress, int x, int y, int mode)
    {
        var width = _memory.Read8(patternAddress);
        var height = _memory.Read8(patternAddress + 1);
        if (width is 0 or > 32 || height == 0)
        {
            return;
        }
        ValidateRectangle(x, y, width, height);

        for (var row = 0; row < height; row++)
        {
            var pattern = ReadPatternWord(patternAddress + 2u + (uint)(row * ((width + 7) / 8)), width);
            for (var column = 0; column < width; column++)
            {
                if ((pattern & (1u << column)) == 0)
                {
                    continue;
                }
                ApplyPixel(x + column, y + row, color, mode);
            }
        }
    }

    public void CopyBlock(int sourceX, int sourceY, int width, int height, int destinationX, int destinationY, int mode)
    {
        ValidateRectangle(sourceX, sourceY, width, height);
        ValidateRectangle(destinationX, destinationY, width, height);
        if (mode != Display.Replace)
        {
            return;
        }

        if (width == 0 || height == 0)
        {
            return;
        }

        var wordsPerRow = (width + 31) / 32;
        var snapshot = ArrayPool<uint>.Shared.Rent(checked(wordsPerRow * height));
        try
        {
            for (var row = 0; row < height; row++)
            {
                for (var word = 0; word < wordsPerRow; word++)
                {
                    var bitOffset = word * 32;
                    var bitCount = Math.Min(32, width - bitOffset);
                    snapshot[row * wordsPerRow + word] = ReadBits(
                        sourceX + bitOffset,
                        sourceY + row,
                        bitCount);
                }
            }

            for (var row = 0; row < height; row++)
            {
                for (var word = 0; word < wordsPerRow; word++)
                {
                    var bitOffset = word * 32;
                    var bitCount = Math.Min(32, width - bitOffset);
                    WriteBits(
                        destinationX + bitOffset,
                        destinationY + row,
                        snapshot[row * wordsPerRow + word],
                        bitCount);
                }
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(snapshot);
        }
    }

    public void ReplPattern(int color, uint patternAddress, int x, int y, int width, int height, int mode)
    {
        ValidateRectangle(x, y, width, height);
        if (mode != Display.Invert)
        {
            return;
        }
        var patternHeight = _memory.Read8(patternAddress + 1);
        if (patternHeight == 0)
        {
            return;
        }

        for (var row = 0; row < height; row++)
        {
            var pattern = _memory.Read32(patternAddress + 4u + (uint)((row % patternHeight) * 4));
            for (var column = 0; column < width; column++)
            {
                if ((pattern & (1u << ((x + column) & 31))) != 0)
                {
                    TogglePixel(x + column, y + row);
                }
            }
        }
    }

    private uint ReadPatternWord(uint address, int width)
    {
        var word = 0u;
        for (var index = 0; index < (width + 7) / 8; index++)
        {
            word |= (uint)_memory.Read8(address + (uint)index) << (index * 8);
        }
        return word;
    }

    private uint ReadBits(int x, int y, int count)
    {
        var shift = x & 31;
        var bits = _memory.Read32(PixelAddress(x, y)) >> shift;
        if (shift != 0 && shift + count > 32)
        {
            bits |= _memory.Read32(PixelAddress(x + 32 - shift, y)) << (32 - shift);
        }

        return bits & LowBits(count);
    }

    private void WriteBits(int x, int y, uint bits, int count)
    {
        var shift = x & 31;
        var firstCount = Math.Min(count, 32 - shift);
        var firstMask = LowBits(firstCount) << shift;
        var firstAddress = PixelAddress(x, y);
        var firstWord = _memory.Read32(firstAddress);
        _memory.Write32(firstAddress, (firstWord & ~firstMask) | ((bits << shift) & firstMask));

        if (firstCount < count)
        {
            var secondCount = count - firstCount;
            var secondMask = LowBits(secondCount);
            var secondAddress = PixelAddress(x + firstCount, y);
            var secondWord = _memory.Read32(secondAddress);
            _memory.Write32(secondAddress, (secondWord & ~secondMask) | ((bits >> firstCount) & secondMask));
        }
    }

    private static uint LowBits(int count) => count == 32 ? uint.MaxValue : (1u << count) - 1;
}
