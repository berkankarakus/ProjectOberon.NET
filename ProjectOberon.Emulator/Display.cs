namespace ProjectOberon.Emulator;

public static class Display
{
    public const uint Base = 0x000e_7f00u;
    public const int Width = 1024;
    public const int Height = 768;
    public const int RowBytes = Width / 8;

    public const int Black = 0;
    public const int White = 1;
    public const int Replace = 0;
    public const int Paint = 1;
    public const int Invert = 2;
}

public sealed class OberonDisplay
{
    private readonly Memory _memory;

    public OberonDisplay(Memory memory) => _memory = memory ?? throw new ArgumentNullException(nameof(memory));

    public void Dot(int color, int x, int y, int mode)
    {
        ValidatePoint(x, y);
        ApplyPixel(x, y, color, mode);
    }

    public void ReplConst(int color, int x, int y, int width, int height, int mode)
    {
        ValidateRectangle(x, y, width, height);
        for (var row = y; row < y + height; row++)
        for (var column = x; column < x + width; column++)
            ApplyPixel(column, row, color, mode);
    }

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

    private void ApplyPixel(int x, int y, int color, int mode)
    {
        switch (mode)
        {
            case Display.Paint: SetPixel(x, y, true); break;
            case Display.Invert: TogglePixel(x, y); break;
            case Display.Replace: SetPixel(x, y, color != Display.Black); break;
            default: throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private bool GetPixel(int x, int y)
    {
        var address = PixelAddress(x, y);
        return (_memory.Read32(address) & (1u << (x & 31))) != 0;
    }

    private void SetPixel(int x, int y, bool value)
    {
        var address = PixelAddress(x, y);
        var mask = 1u << (x & 31);
        var word = _memory.Read32(address);
        _memory.Write32(address, value ? word | mask : word & ~mask);
    }

    private void TogglePixel(int x, int y)
    {
        var address = PixelAddress(x, y);
        _memory.Write32(address, _memory.Read32(address) ^ (1u << (x & 31)));
    }

    private uint ReadPatternWord(uint address, int width)
    {
        var word = 0u;
        for (var index = 0; index < (width + 7) / 8; index++) word |= (uint)_memory.Read8(address + (uint)index) << (index * 8);
        return word;
    }

    private static uint PixelAddress(int x, int y) => Display.Base + (uint)(y * Display.RowBytes + (x / 32) * sizeof(uint));

    private static void ValidatePoint(int x, int y)
    {
        if ((uint)x >= Display.Width || (uint)y >= Display.Height) throw new ArgumentOutOfRangeException();
    }

    private static void ValidateRectangle(int x, int y, int width, int height)
    {
        if (width < 0 || height < 0 || x < 0 || y < 0 || x > Display.Width - width || y > Display.Height - height)
            throw new ArgumentOutOfRangeException();
    }
}
