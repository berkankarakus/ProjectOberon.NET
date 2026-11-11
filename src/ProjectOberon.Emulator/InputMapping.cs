namespace ProjectOberon.Emulator;

public readonly record struct KeyboardKey(byte Code, bool Extended);

public static class InputMapping
{
    private static readonly byte[] LetterCodes = [0x1c, 0x32, 0x21, 0x23, 0x24, 0x2b, 0x34, 0x33, 0x43, 0x3b, 0x42, 0x4b, 0x3a, 0x31, 0x44, 0x4d, 0x15, 0x2d, 0x1b, 0x2c, 0x3c, 0x2a, 0x1d, 0x22, 0x35, 0x1a];

    public static KeyboardKey? KeyboardKey(int key)
    {
        if (key is >= 'a' and <= 'z')
        {
            return new KeyboardKey(LetterCodes[key - 'a'], false);
        }
        return key switch
        {
            '1' => new(0x16, false), '2' => new(0x1e, false), '3' => new(0x26, false), '4' => new(0x25, false),
            '5' => new(0x2e, false), '6' => new(0x36, false), '7' => new(0x3d, false), '8' => new(0x3e, false),
            '9' => new(0x46, false), '0' => new(0x45, false),
            '-' => new(0x4e, false), '=' => new(0x55, false), '[' => new(0x54, false), ']' => new(0x5b, false),
            '\\' => new(0x5d, false), ';' => new(0x4c, false), '\'' => new(0x52, false), '`' => new(0x0e, false),
            ',' => new(0x41, false), '.' => new(0x49, false), '/' => new(0x4a, false), ' ' => new(0x29, false),
            13 => new(0x5a, false), 9 => new(0x0d, false), 8 => new(0x66, false), 27 => new(0x76, false),
            0x40000039 => new(0x58, false),
            0x400000e1 => new(0x12, false), 0x400000e5 => new(0x59, false),
            0x400000e0 => new(0x14, false), 0x400000e4 => new(0x14, true),
            0x400000e2 => new(0x11, false), 0x400000e6 => new(0x11, true),
            0x4000003a => new(0x05, false), 0x4000003b => new(0x06, false),
            0x4000003c => new(0x04, false), 0x4000003d => new(0x0c, false),
            0x4000003e => new(0x03, false), 0x4000003f => new(0x0b, false),
            0x40000040 => new(0x83, false), 0x40000041 => new(0x0a, false),
            0x40000042 => new(0x01, false), 0x40000043 => new(0x09, false),
            0x40000044 => new(0x78, false), 0x40000045 => new(0x07, false),
            0x40000052 => new(0x75, true), 0x40000051 => new(0x72, true),
            0x40000050 => new(0x6b, true), 0x4000004f => new(0x74, true),
            0x40000049 => new(0x70, true), 0x4000004c => new(0x71, true),
            0x4000004a => new(0x6c, true), 0x4000004d => new(0x69, true),
            0x4000004b => new(0x7d, true), 0x4000004e => new(0x7a, true),
            _ => null
        };
    }

    public static byte MouseButtonsFromState(uint state)
    {
        var buttons = 0;
        if ((state & 1) != 0) buttons |= 0x04;
        if ((state & 2) != 0) buttons |= 0x02;
        if ((state & 4) != 0) buttons |= 0x01;
        return (byte)buttons;
    }

    public static byte MouseButtonMask(byte button) => button switch { 1 => 0x04, 2 => 0x02, 3 => 0x01, _ => 0 };

    public static ushort ScaleCoordinate(int coordinate, int extent, int maximum)
    {
        if (extent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(extent));
        }

        if (maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        if (extent == 1)
        {
            return 0;
        }
        var clamped = Math.Clamp(coordinate, 0, extent - 1);
        return (ushort)(clamped * (long)(maximum - 1) / (extent - 1));
    }
}
