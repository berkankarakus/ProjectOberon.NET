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
        var firstWord = x & ~31;
        var lastWord = (right - 1) & ~31;
        var firstMask = uint.MaxValue << (x & 31);
        var lastMask = LowBits(((right - 1) & 31) + 1);
        for (var row = y; row < y + height; row++)
        {
            if (firstWord == lastWord)
            {
                ApplyWordMask(PixelAddress(firstWord, row), firstMask & lastMask, color, mode);
                continue;
            }

            ApplyWordMask(PixelAddress(firstWord, row), firstMask, color, mode);
            for (var wordX = firstWord + 32; wordX < lastWord; wordX += 32)
            {
                ApplyWordMask(PixelAddress(wordX, row), uint.MaxValue, color, mode);
            }

            ApplyWordMask(PixelAddress(lastWord, row), lastMask, color, mode);
        }
    }
}
