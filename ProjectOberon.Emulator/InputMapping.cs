namespace ProjectOberon.Emulator;

public readonly record struct KeyboardKey(byte Code, bool Extended);

public static class InputMapping
{
    private static readonly byte[] LetterCodes = [0x1c, 0x32, 0x21, 0x23, 0x24, 0x2b, 0x34, 0x33, 0x43, 0x3b, 0x42, 0x4b, 0x3a, 0x31, 0x44, 0x4d, 0x15, 0x2d, 0x1b, 0x2c, 0x3c, 0x2a, 0x1d, 0x22, 0x35, 0x1a];

    public static KeyboardKey? KeyboardKey(int key)
    {
        if (key is >= 'a' and <= 'z') return new KeyboardKey(LetterCodes[key - 'a'], false);
        return key switch
        {
            '1' => new(0x16, false), '2' => new(0x1e, false), '3' => new(0x26, false), '4' => new(0x25, false),
            '5' => new(0x2e, false), '6' => new(0x36, false), '7' => new(0x3d, false), '8' => new(0x3e, false),
            '9' => new(0x46, false), '0' => new(0x45, false),
            '-' => new(0x4e, false), '=' => new(0x55, false), '[' => new(0x54, false), ']' => new(0x5b, false),
            '\\' => new(0x5d, false), ';' => new(0x4c, false), '\'' => new(0x52, false), '`' => new(0x0e, false),
            ',' => new(0x41, false), '.' => new(0x49, false), '/' => new(0x4a, false), ' ' => new(0x29, false),
            13 => new(0x5a, false), 9 => new(0x0d, false), 8 => new(0x66, false), 27 => new(0x76, false),
            0x40000042 => new(0x01, false), 0x40000043 => new(0x09, false),
            0x40000052 => new(0x75, true), 0x40000051 => new(0x72, true),
            0x40000050 => new(0x6b, true), 0x4000004f => new(0x74, true),
            _ => null
        };
    }

    public static byte MouseButtonsFromState(uint state) => (byte)(((state & 1) != 0 ? 0x04 : 0) | ((state & 2) != 0 ? 0x02 : 0) | ((state & 4) != 0 ? 0x01 : 0));

    public static byte MouseButtonMask(byte button) => button switch { 1 => 0x04, 2 => 0x02, 3 => 0x01, _ => 0 };

    public static ushort ScaleCoordinate(int coordinate, int extent, int maximum)
    {
        if (extent <= 1) return 0;
        var clamped = Math.Clamp(coordinate, 0, extent - 1);
        return (ushort)(clamped * (long)(maximum - 1) / (extent - 1));
    }
}
