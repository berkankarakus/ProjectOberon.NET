namespace ProjectOberon.Emulator;

public sealed partial class OberonDisplay
{
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

    private void ApplyWordMask(uint address, uint mask, int color, int mode)
    {
        var word = _memory.Read32(address);
        word = mode switch
        {
            Display.Paint => word | mask,
            Display.Invert => word ^ mask,
            Display.Replace when color == Display.Black => word & ~mask,
            Display.Replace => word | mask,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        _memory.Write32(address, word);
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

    private static uint PixelAddress(int x, int y) =>
        Display.Base + (uint)(y * Display.RowBytes + (x / 32) * sizeof(uint));

    private static void ValidatePoint(int x, int y)
    {
        if ((uint)x >= Display.Width || (uint)y >= Display.Height)
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    private static void ValidateRectangle(int x, int y, int width, int height)
    {
        if (width < 0 || height < 0 || x < 0 || y < 0 ||
            x > Display.Width - width || y > Display.Height - height)
        {
            throw new ArgumentOutOfRangeException();
        }
    }
}
