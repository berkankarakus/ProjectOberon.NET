using System.Globalization;
using System.Runtime.InteropServices;
using ProjectOberon.Emulator;
using ProjectOberon.SdlUi;

try
{
    var options = UiOptions.Parse(args);
    if (options.Help)
    {
        UiOptions.PrintUsage();
        return;
    }

    using var machine = new Machine();
    if (options.Rom is not null) machine.Memory.LoadRomFile(options.Rom);
    if (options.Disk is not null) machine.OpenDisk(options.Disk, options.DiskWritable);
    Run(machine, options);
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or DllNotFoundException)
{
    Console.Error.WriteLine($"project-oberon-ui: {error.Message}");
    Environment.ExitCode = 1;
}

static void Run(Machine machine, UiOptions options)
{
    if (SdlNative.SDL_Init(SdlNative.InitVideo) != 0) throw new InvalidOperationException(SdlNative.Error());
    IntPtr window = IntPtr.Zero, renderer = IntPtr.Zero, texture = IntPtr.Zero;
    try
    {
        var width = Math.Max(1, (int)(Display.Width * options.Scale));
        var height = Math.Max(1, (int)(Display.Height * options.Scale));
        window = SdlNative.SDL_CreateWindow("Project Oberon", SdlNative.WindowPositionCentered, SdlNative.WindowPositionCentered, width, height, SdlNative.WindowResizable);
        if (window == IntPtr.Zero) throw new InvalidOperationException(SdlNative.Error());
        renderer = SdlNative.SDL_CreateRenderer(window, -1, SdlNative.RendererAccelerated | SdlNative.RendererPresentVsync);
        if (renderer == IntPtr.Zero) renderer = SdlNative.SDL_CreateRenderer(window, -1, 0);
        if (renderer == IntPtr.Zero) throw new InvalidOperationException(SdlNative.Error());
        texture = SdlNative.SDL_CreateTexture(renderer, SdlNative.PixelFormatArgb8888, (int)SdlNative.TextureAccessStreaming, Display.Width, Display.Height);
        if (texture == IntPtr.Zero) throw new InvalidOperationException(SdlNative.Error());

        var pixels = new uint[Display.Width * Display.Height];
        var running = true;
        var paused = false;
        byte mouseButtons = 0;
        UpdateMouse(machine.Memory, width / 2, height / 2, width, height, options.FlipY, mouseButtons);

        while (running)
        {
            while (SdlNative.SDL_PollEvent(out var sdlEvent) != 0)
            {
                switch (sdlEvent.Type)
                {
                    case SdlNative.Quit:
                        running = false;
                        break;
                    case SdlNative.WindowEvent when sdlEvent.Window.Event == SdlNative.WindowEventSizeChanged:
                        width = Math.Max(1, sdlEvent.Window.Data1);
                        height = Math.Max(1, sdlEvent.Window.Data2);
                        break;
                    case SdlNative.MouseMotion:
                        UpdateMouse(machine.Memory, sdlEvent.Motion.X, sdlEvent.Motion.Y, width, height, options.FlipY,
                            InputMapping.MouseButtonsFromState(sdlEvent.Motion.State));
                        break;
                    case SdlNative.MouseButtonDown:
                    case SdlNative.MouseButtonUp:
                        var mask = InputMapping.MouseButtonMask(sdlEvent.Button.Button);
                        if (sdlEvent.Type == SdlNative.MouseButtonDown) mouseButtons |= mask;
                        else mouseButtons &= (byte)~mask;
                        UpdateMouse(machine.Memory, sdlEvent.Button.X, sdlEvent.Button.Y, width, height, options.FlipY, mouseButtons);
                        break;
                    case SdlNative.KeyDown:
                    case SdlNative.KeyUp:
                        if (sdlEvent.Key.Repeat != 0) break;
                        var key = sdlEvent.Key.Keysym.Sym;
                        if (sdlEvent.Type == SdlNative.KeyDown && key == 27) running = false;
                        else if (sdlEvent.Type == SdlNative.KeyDown && key == 0x40000042) paused = !paused;
                        else if (sdlEvent.Type == SdlNative.KeyDown && key == 0x40000043) machine.Step();
                        else EnqueueKeyboard(machine.Memory, key, sdlEvent.Type == SdlNative.KeyDown);
                        break;
                }
            }

            if (!paused) machine.Run(options.StepsPerFrame);
            var uart = machine.Memory.TakeUartOutput();
            var litPixels = UpdateFramebuffer(machine.Memory, pixels, options.FlipY);
            SdlNative.SDL_SetWindowTitle(window, WindowTitle(machine, paused, uart, litPixels));
            if (SdlNative.SDL_UpdateTexture(texture, IntPtr.Zero, pixels, Display.Width * sizeof(uint)) != 0) throw new InvalidOperationException(SdlNative.Error());
            SdlNative.SDL_RenderClear(renderer);
            SdlNative.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SdlNative.SDL_RenderPresent(renderer);
            SdlNative.SDL_Delay(1);
        }
    }
    finally
    {
        if (texture != IntPtr.Zero) SdlNative.SDL_DestroyTexture(texture);
        if (renderer != IntPtr.Zero) SdlNative.SDL_DestroyRenderer(renderer);
        if (window != IntPtr.Zero) SdlNative.SDL_DestroyWindow(window);
        SdlNative.SDL_Quit();
    }
}

static int UpdateFramebuffer(Memory memory, uint[] pixels, bool flipY)
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

static void EnqueueKeyboard(Memory memory, int key, bool pressed)
{
    var mapped = InputMapping.KeyboardKey(key);
    if (mapped is not KeyboardKey keyboard) return;
    if (keyboard.Extended) memory.EnqueueKeyboard(0xe0);
    if (!pressed) memory.EnqueueKeyboard(0xf0);
    memory.EnqueueKeyboard(keyboard.Code);
}

static void UpdateMouse(Memory memory, int x, int y, int width, int height, bool flipY, byte buttons)
{
    var screenX = InputMapping.ScaleCoordinate(x, width, Display.Width);
    var screenY = InputMapping.ScaleCoordinate(y, height, Display.Height);
    var memoryY = flipY ? (ushort)(Display.Height - 1 - screenY) : screenY;
    memory.SetMouse(screenX, memoryY, buttons);
}

static string WindowTitle(Machine machine, bool paused, string uart, int litPixels)
{
    var suffix = uart.Length == 0 ? string.Empty : $" | uart=\"{uart[^Math.Min(32, uart.Length)..].Replace('\n', ' ').Replace('\r', ' ')}\"";
    return $"Project Oberon | {(paused ? "paused" : "running")} | pc=0x{machine.Cpu.PcByte:x6} | steps={machine.Cpu.Steps} | fb={litPixels}{suffix}";
}

internal sealed record UiOptions(string? Rom, string? Disk, bool DiskWritable, ulong StepsPerFrame, double Scale, bool FlipY, bool Help)
{
    public static UiOptions Parse(string[] args)
    {
        string? rom = null, disk = null;
        var writable = true;
        var steps = 100_000UL;
        var scale = 1.25;
        var flipY = true;
        var help = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help": case "-h": help = true; break;
                case "--rom" when index + 1 < args.Length: rom = args[++index]; break;
                case "--disk" when index + 1 < args.Length: disk = args[++index]; break;
                case "--read-only": writable = false; break;
                case "--steps-per-frame" when index + 1 < args.Length:
                    if (!ulong.TryParse(args[++index], out steps)) throw new ArgumentException("--steps-per-frame must be an unsigned integer");
                    break;
                case "--scale" when index + 1 < args.Length:
                    if (!double.TryParse(args[++index], NumberStyles.Float, CultureInfo.InvariantCulture, out scale) || scale < 0.5 || scale > 4.0)
                        throw new ArgumentException("--scale must be between 0.5 and 4.0");
                    break;
                case "--no-flip-y": flipY = false; break;
                default: throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }
        if (!writable && disk is null) throw new ArgumentException("--read-only requires --disk FILE");
        return new UiOptions(rom, disk, writable, steps, scale, flipY, help);
    }

    public static void PrintUsage() => Console.Error.WriteLine("usage: project-oberon-ui [--rom FILE] [--disk FILE] [--read-only] [--steps-per-frame N] [--scale N] [--no-flip-y]");
}
