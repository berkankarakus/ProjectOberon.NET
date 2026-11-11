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
        for (var row = y; row < y + height; row++)
        for (var column = x; column < x + width; column++)
            ApplyPixel(column, row, color, mode);
    }
}
