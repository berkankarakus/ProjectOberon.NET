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

public sealed partial class OberonDisplay
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
        if (width == 0 || height == 0)
        {
            return;
        }

        var right = x + width;
        for (var row = y; row < y + height; row++)
        {
            for (var wordX = x & ~31; wordX < right; wordX += 32)
            {
                var start = Math.Max(x, wordX);
                var end = Math.Min(right, wordX + 32);
                var length = end - start;
                var mask = length == 32
                    ? uint.MaxValue
                    : ((1u << length) - 1) << (start & 31);
                ApplyWordMask(PixelAddress(wordX, row), mask, color, mode);
            }
        }
    }
}
