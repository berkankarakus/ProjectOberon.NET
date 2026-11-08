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
        var memoryY = flipY ? (ushort)(Display.Height - 1 - screenY) : screenY;
        memory.SetMouse(screenX, memoryY, buttons);
    }

    public static string WindowTitle(Machine machine, bool paused, string uart, int litPixels)
    {
        var suffix = uart.Length == 0 ? string.Empty : $" | uart=\"{uart[^Math.Min(32, uart.Length)..].Replace('\n', ' ').Replace('\r', ' ')}\"";
        return $"Project Oberon | {(paused ? "paused" : "running")} | pc=0x{machine.Cpu.PcByte:x6} | steps={machine.Cpu.Steps} | fb={litPixels}{suffix}";
    }

    public static void DrawHostCursor(IntPtr renderer, int x, int y)
    {
        var outline = new[]
        {
            (x, y), (x, y + 18), (x + 5, y + 13), (x + 10, y + 23),
            (x + 14, y + 21), (x + 9, y + 11), (x + 17, y + 11), (x, y)
        };
        var inner = new[]
        {
            (x + 2, y + 4), (x + 2, y + 13), (x + 6, y + 10), (x + 11, y + 20),
            (x + 12, y + 19), (x + 7, y + 9), (x + 13, y + 9), (x + 2, y + 4)
        };

        SdlNative.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
        DrawLines(renderer, outline);
        SdlNative.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
        DrawLines(renderer, inner);
    }

    private static void DrawLines(IntPtr renderer, (int X, int Y)[] points)
    {
        for (var index = 1; index < points.Length; index++)
        {
            SdlNative.SDL_RenderDrawLine(renderer,
                points[index - 1].X, points[index - 1].Y,
                points[index].X, points[index].Y);
        }
    }
}
