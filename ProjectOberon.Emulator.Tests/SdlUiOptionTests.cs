using ProjectOberon.Emulator;
using ProjectOberon.SdlUi;

internal static partial class SdlUiTests
{
    private static void ParsesUiOptions()
    {
        var options = UiOptions.Parse(["--rom", "prom.mem", "--disk", "work.img", "--read-only", "--steps-per-frame", "200", "--scale", "2", "--no-flip-y", "--editor", "notes.txt", "--font", "Oberon10.Scn.Fnt", "--start-module", "Input", "--initialize", "--command", "Start"]);

        TestAssertions.Equal("prom.mem", options.Rom);
        TestAssertions.Equal("work.img", options.Disk);
        TestAssertions.False(options.DiskWritable);
        TestAssertions.Equal(200UL, options.StepsPerFrame);
        TestAssertions.Equal(2d, options.Scale);
        TestAssertions.False(options.FlipY);
        TestAssertions.Equal("notes.txt", options.Editor);
        TestAssertions.Equal("Oberon10.Scn.Fnt", options.Font);
        TestAssertions.Equal("Input", options.StartModule);
        TestAssertions.True(options.Initialize);
        TestAssertions.Equal("Start", options.Command);
    }

    private static void RejectsInvalidUiOptions()
    {
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--read-only"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--scale", "0.4"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--scale", "4.1"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--steps-per-frame", "x"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--unknown"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--disk"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--font", "Oberon10.Scn.Fnt"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--start-module", "Input"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--initialize"]));
        TestAssertions.Throws<ArgumentException>(() => UiOptions.Parse(["--command", "Start"]));
    }

    private static void EncodesKeyboardEvents()
    {
        var memory = new Memory();
        UiLogic.EnqueueKeyboard(memory, ' ', pressed: true);
        TestAssertions.Equal(0x29u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));

        UiLogic.EnqueueKeyboard(memory, 0x40000052, pressed: true);
        TestAssertions.Equal(0xe0u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));
        TestAssertions.Equal(0x75u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));

        UiLogic.EnqueueKeyboard(memory, ' ', pressed: false);
        TestAssertions.Equal(0xf0u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));
        TestAssertions.Equal(0x29u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));
    }

    private static void ScalesMouseCoordinates()
    {
        var memory = new Memory();
        UiLogic.UpdateMouse(memory, 50, 25, 100, 50, flipY: true, buttons: 5);
        var status = memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus);
        TestAssertions.Equal((5u << 24) | (376u << 12) | 516u, status & 0x0fff_ffffu | (5u << 24));
    }

    private static void RendersFramebufferPixels()
    {
        var memory = new Memory();
        memory.Write32(Display.Base, 1);
        var pixels = new uint[Display.Width * Display.Height];
        var lit = UiLogic.UpdateFramebuffer(memory, pixels, flipY: false);

        TestAssertions.Equal(1, lit);
        TestAssertions.Equal(0xffff_ffffu, pixels[0]);
        TestAssertions.Equal(0xff00_0000u, pixels[1]);
    }

    private static void FormatsWindowTitle()
    {
        var title = UiLogic.WindowTitle(new Machine(), paused: true, uart: "hello\n", litPixels: 7);

        TestAssertions.True(title.StartsWith("Project Oberon | paused |", StringComparison.Ordinal));
        TestAssertions.True(title.Contains("steps=0", StringComparison.Ordinal));
        TestAssertions.True(title.Contains("fb=7", StringComparison.Ordinal));
        TestAssertions.True(title.Contains("uart=\"hello \"", StringComparison.Ordinal));
    }
}
