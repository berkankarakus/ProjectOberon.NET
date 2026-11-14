using ProjectOberon.Emulator;

internal static partial class DeviceTests
{
    public static void DisplayAndInputMapping()
    {
        TestAssertions.Equal(0x000e_7f00u, Display.Base);
        TestAssertions.Equal(1024, Display.Width);
        TestAssertions.Equal(768, Display.Height);
        TestAssertions.Equal(0x29, InputMapping.KeyboardKey(' ')!.Value.Code);
        TestAssertions.Equal(0x75, InputMapping.KeyboardKey(0x40000052)!.Value.Code);
        TestAssertions.True(InputMapping.KeyboardKey(0x40000052)!.Value.Extended);
        TestAssertions.Equal(0x12, InputMapping.KeyboardKey(0x400000e1)!.Value.Code);
        TestAssertions.Equal(0x70, InputMapping.KeyboardKey(0x40000049)!.Value.Code);
        TestAssertions.Equal(0x05, InputMapping.MouseButtonsFromState(1 | 4));
        TestAssertions.Equal(1023, InputMapping.ScaleCoordinate(999, 1000, Display.Width));

        var memory = new Memory();
        memory.EnqueueUartRx((byte)'O');
        TestAssertions.Equal(3u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.UartControl));
        TestAssertions.Equal((uint)'O', memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.UartData));

        var display = new OberonDisplay(memory);
        display.Dot(Display.White, 0, 0, Display.Replace);
        TestAssertions.Equal(1u, memory.Read32(Display.Base));
        display.Dot(Display.White, 0, 0, Display.Invert);
        TestAssertions.Equal(0u, memory.Read32(Display.Base));
        display.ReplConst(Display.White, 30, 1, 4, 1, Display.Replace);
        TestAssertions.Equal(0xc000_0000u, memory.Read32(Display.Base + (uint)Display.RowBytes));
        TestAssertions.Equal(0x0000_0003u, memory.Read32(Display.Base + (uint)Display.RowBytes + 4));
        memory.Write8(0x100, 3);
        memory.Write8(0x101, 2);
        memory.Write8(0x102, 0b101);
        memory.Write8(0x103, 0b010);
        display.CopyPattern(Display.White, 0x100, 10, 2, Display.Paint);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(2 * Display.RowBytes)) & (1u << 10)) != 0);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(3 * Display.RowBytes)) & (1u << 11)) != 0);
        display.CopyBlock(10, 2, 3, 2, 20, 4, Display.Replace);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(4 * Display.RowBytes)) & (1u << 20)) != 0);
        TestAssertions.True((memory.Read32(Display.Base + (uint)(5 * Display.RowBytes)) & (1u << 21)) != 0);
    }

    public static void OberonInputDecoder()
    {
        var keyboard = new OberonKeyboard();
        keyboard.Enqueue(0x1c);
        TestAssertions.Equal(1, keyboard.Available());
        TestAssertions.True(keyboard.TryRead(out var character));
        TestAssertions.Equal('a', character);
        keyboard.Enqueue(0x12);
        keyboard.Enqueue(0x1c);
        keyboard.Enqueue(0xf0);
        keyboard.Enqueue(0x1c);
        keyboard.Enqueue(0xf0);
        keyboard.Enqueue(0x12);
        TestAssertions.True(keyboard.TryRead(out character));
        TestAssertions.Equal('A', character);
        TestAssertions.Equal(0, keyboard.Available());
        keyboard.Enqueue(0x14);
        keyboard.Enqueue(0x21);
        TestAssertions.True(keyboard.TryRead(out character));
        TestAssertions.Equal('\x03', character);
    }

    public static void OberonKernelSectors()
    {
        var memory = new Memory();
        var kernel = new OberonKernel(memory);
        TestAssertions.Equal(0, kernel.AllocatedSectors);
        TestAssertions.Equal(0x80000u, OberonKernel.PhysicalSector(0));
        TestAssertions.Equal(0x80002u, OberonKernel.PhysicalSector(29));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => kernel.MarkSector(-29));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => kernel.MarkSector(1));
        TestAssertions.Throws<ArgumentOutOfRangeException>(() => kernel.MarkSector(OberonKernel.MapSize * 29));
        TestAssertions.Equal(64 * 29, kernel.AllocSector(0));
        TestAssertions.Equal(1, kernel.AllocatedSectors);
        kernel.MarkSector(100 * 29);
        TestAssertions.Equal(2, kernel.AllocatedSectors);
        kernel.MarkSector(100 * 29);
        TestAssertions.Equal(2, kernel.AllocatedSectors);
        kernel.FreeSector(100 * 29);
        TestAssertions.Equal(1, kernel.AllocatedSectors);
        kernel.FreeSector(100 * 29);
        TestAssertions.Equal(1, kernel.AllocatedSectors);
        kernel.SetClock(1234);
        TestAssertions.Equal(1234, kernel.Clock);
        memory.AdvanceTime();
        TestAssertions.Equal(1, kernel.Time);
    }
}
