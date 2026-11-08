using ProjectOberon.Emulator;

namespace ProjectOberon.SdlUi;

internal static class UiLogic
{
    public static int UpdateFramebuffer(Memory memory, uint[] pixels, bool flipY)
    {
        var lit = 0;
        for (var y = 0; y < Display.Height; y++)
        {
            var row = Display.Base + (uint)(y * Display.RowBytes);
            for (var word = 0; word < Display.Width / 32; word++)
            {
                var bits = memory.Read32(row + (uint)(word * sizeof(uint)));
                var screenY = flipY ? Display.Height - 1 - y : y;
                for (var bit = 0; bit < 32; bit++)
                {
                    var x = word * 32 + bit;
                    var on = (bits & (1u << bit)) != 0;
                    if (on) lit++;
                    pixels[screenY * Display.Width + x] = on ? 0xffff_ffffu : 0xff00_0000u;
                }
            }
        }
        return lit;
    }

    public static bool EnqueueKeyboard(Memory memory, int key, bool pressed)
    {
        var mapped = InputMapping.KeyboardKey(key);
        if (mapped is not KeyboardKey keyboard)
        {
            return false;
        }
        if (keyboard.Extended) memory.EnqueueKeyboard(0xe0);
        if (!pressed) memory.EnqueueKeyboard(0xf0);
        memory.EnqueueKeyboard(keyboard.Code);
        return true;
    }

    public static unsafe void EnqueueTextInput(Memory memory, SdlNative.TextInputEvent text)
    {
        foreach (var character in System.Text.Encoding.UTF8.GetBytes(ReadTextInput(text))) memory.EnqueueUartRx(character);
    }

    public static unsafe string ReadTextInput(SdlNative.TextInputEvent text)
    {
        Span<byte> bytes = stackalloc byte[32];
        var length = 0;
        for (; length < bytes.Length && text.Text[length] != 0; length++) bytes[length] = text.Text[length];
        return System.Text.Encoding.UTF8.GetString(bytes[..length]);
    }

    public static void UpdateMouse(Memory memory, int x, int y, int width, int height, bool flipY, byte buttons)
    {
        var screenX = InputMapping.ScaleCoordinate(x, width, Display.Width);
        var screenY = InputMapping.ScaleCoordinate(y, height, Display.Height);
        memory.SetMouse(screenX, screenY, buttons);
    }

    public static string WindowTitle(Machine machine, bool paused, string uart, int litPixels)
    {
        var suffix = uart.Length == 0 ? string.Empty : $" | uart=\"{uart[^Math.Min(32, uart.Length)..].Replace('\n', ' ').Replace('\r', ' ')}\"";
        return $"Project Oberon | {(paused ? "paused" : "running")} | pc=0x{machine.Cpu.PcByte:x6} | steps={machine.Cpu.Steps} | fb={litPixels}{suffix}";
    }
}
