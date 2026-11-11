using ProjectOberon.Emulator;

internal static class InputMappingTests
{
    public static void Run()
    {
        MapsLettersAndSpecialKeys();
        MapsMouseButtons();
        ScalesAndClampsCoordinates();
    }

    private static void MapsLettersAndSpecialKeys()
    {
        TestAssertions.Equal(new KeyboardKey(0x1c, false), InputMapping.KeyboardKey('a'));
        TestAssertions.Equal(new KeyboardKey(0x1a, false), InputMapping.KeyboardKey('z'));
        TestAssertions.Equal(new KeyboardKey(0x16, false), InputMapping.KeyboardKey('1'));
        TestAssertions.Equal(new KeyboardKey(0x29, false), InputMapping.KeyboardKey(' '));
        TestAssertions.Equal(new KeyboardKey(0x75, true), InputMapping.KeyboardKey(0x40000052));
        TestAssertions.True(InputMapping.KeyboardKey('A') is null);
        TestAssertions.True(InputMapping.KeyboardKey('?') is null);
    }

    private static void MapsMouseButtons()
    {
        TestAssertions.Equal((byte)0, InputMapping.MouseButtonsFromState(0));
        TestAssertions.Equal((byte)0x07, InputMapping.MouseButtonsFromState(7));
        TestAssertions.Equal((byte)0x04, InputMapping.MouseButtonMask(1));
        TestAssertions.Equal((byte)0x02, InputMapping.MouseButtonMask(2));
        TestAssertions.Equal((byte)0x01, InputMapping.MouseButtonMask(3));
        TestAssertions.Equal((byte)0, InputMapping.MouseButtonMask(4));
    }

    private static void ScalesAndClampsCoordinates()
    {
        TestAssertions.Equal((ushort)0, InputMapping.ScaleCoordinate(-10, 100, 1024));
        TestAssertions.Equal((ushort)1023, InputMapping.ScaleCoordinate(200, 100, 1024));
        TestAssertions.Equal((ushort)516, InputMapping.ScaleCoordinate(50, 100, 1024));
        TestAssertions.Equal((ushort)0, InputMapping.ScaleCoordinate(10, 1, 1024));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => InputMapping.ScaleCoordinate(0, 0, 1024));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => InputMapping.ScaleCoordinate(0, 100, 0));
    }
}
