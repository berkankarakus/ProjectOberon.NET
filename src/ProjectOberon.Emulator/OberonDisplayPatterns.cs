namespace ProjectOberon.Emulator;

public sealed partial class OberonDisplay
{
    public void CopyPattern(int color, uint patternAddress, int x, int y, int mode)
    {
        var width = _memory.Read8(patternAddress);
        var height = _memory.Read8(patternAddress + 1);
        if (width is 0 or > 32 || height == 0) return;
        ValidateRectangle(x, y, width, height);

        for (var row = 0; row < height; row++)
        {
            var pattern = ReadPatternWord(patternAddress + 2u + (uint)(row * ((width + 7) / 8)), width);
            for (var column = 0; column < width; column++)
            {
                if ((pattern & (1u << column)) == 0) continue;
                ApplyPixel(x + column, y + row, color, mode);
            }
        }
    }

    public void CopyBlock(int sourceX, int sourceY, int width, int height, int destinationX, int destinationY, int mode)
    {
        ValidateRectangle(sourceX, sourceY, width, height);
        ValidateRectangle(destinationX, destinationY, width, height);
        if (mode != Display.Replace) return;

        var snapshot = new bool[height, width];
        for (var row = 0; row < height; row++)
        for (var column = 0; column < width; column++)
            snapshot[row, column] = GetPixel(sourceX + column, sourceY + row);

        for (var row = 0; row < height; row++)
        for (var column = 0; column < width; column++)
            SetPixel(destinationX + column, destinationY + row, snapshot[row, column]);
    }

    public void ReplPattern(int color, uint patternAddress, int x, int y, int width, int height, int mode)
    {
        ValidateRectangle(x, y, width, height);
        if (mode != Display.Invert) return;
        var patternHeight = _memory.Read8(patternAddress + 1);
        if (patternHeight == 0) return;

        for (var row = 0; row < height; row++)
        {
            var pattern = _memory.Read32(patternAddress + 4u + (uint)((row % patternHeight) * 4));
            for (var column = 0; column < width; column++)
            {
                if ((pattern & (1u << ((x + column) & 31))) != 0) TogglePixel(x + column, y + row);
            }
        }
    }

    private uint ReadPatternWord(uint address, int width)
    {
        var word = 0u;
        for (var index = 0; index < (width + 7) / 8; index++)
            word |= (uint)_memory.Read8(address + (uint)index) << (index * 8);
        return word;
    }
}
