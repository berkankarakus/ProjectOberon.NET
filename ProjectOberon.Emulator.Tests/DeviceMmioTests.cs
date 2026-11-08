using ProjectOberon.Emulator;

internal static partial class DeviceTests
{
    public static void MmioDevices()
    {
        var memory = new Memory();
        memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.Leds, 0x5a);
        TestAssertions.Equal(0x5au, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Leds));
        memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.UartData, 'O');
        memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.UartData, 'K');
        TestAssertions.Equal("OK", memory.TakeUartOutput());
        memory.EnqueueKeyboard(0x1c);
        TestAssertions.True((memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus) & 0x1000_0000u) != 0);
        TestAssertions.Equal(0x1cu, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));
        memory.SetMouse(123, 456, 5);
        TestAssertions.Equal((5u << 24) | (456u << 12) | 123u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus) & 0x0fff_ffffu | (5u << 24));
    }
}
