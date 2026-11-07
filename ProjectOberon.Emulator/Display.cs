namespace ProjectOberon.Emulator;

public static class Display
{
    public const uint Base = 0x000e_7f00u;
    public const int Width = 1024;
    public const int Height = 768;
    public const int RowBytes = Width / 8;
}
