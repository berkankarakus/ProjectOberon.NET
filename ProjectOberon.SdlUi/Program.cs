using System.Globalization;
using System.Runtime.InteropServices;
using ProjectOberon.Emulator;
using ProjectOberon.SdlUi;

Machine? machine = null;
try
{
    var options = UiOptions.Parse(args);
    if (options.Help)
    {
        UiOptions.PrintUsage();
        return;
    }

    using var log = UiLog.Open(options.LogPath);
    machine = new Machine();
    if (options.Rom is not null) machine.Memory.LoadRomFile(options.Rom);
    if (options.Disk is not null) machine.OpenDisk(options.Disk, options.DiskWritable);
    if (options.StartModule is not null)
    {
        var boot = OberonBootSession.Create(machine);
        if (options.Boot)
        {
            var image = boot.Boot(options.StartModule, options.Command);
            Console.WriteLine($"booted module {image.Module.Name}"
                + (options.Command is null ? string.Empty : $" command {options.Command}")
                + $" at pc=0x{machine.Cpu.PcByte:x6}");
            log.Write($"boot module={image.Module.Name} pc=0x{machine.Cpu.PcByte:x6}");
        }
        else
        {
            if (options.Initialize)
            {
                var descriptor = boot.Initialize(options.StartModule);
                Console.WriteLine($"initialized module {options.StartModule} descriptor=0x{descriptor:x8}");
                log.Write($"initialize module={options.StartModule} descriptor=0x{descriptor:x8}");
            }

            var image = boot.Start(options.StartModule, options.Command);
            Console.WriteLine($"started module {image.Module.Name}"
                + (options.Command is null ? string.Empty : $" command {options.Command}")
                + $" at pc=0x{machine.Cpu.PcByte:x6}");
            log.Write($"start module={image.Module.Name} pc=0x{machine.Cpu.PcByte:x6}");
        }
    }
    Run(machine, options, log);
}
catch (Exception error)
{
    Console.Error.WriteLine(OberonRuntimeDiagnostics.Format("project-oberon-ui", machine, error));
    Environment.ExitCode = 1;
}
finally
{
    machine?.Dispose();
}
static void Run(Machine machine, UiOptions options, UiLog log)
{
    if (SdlNative.SDL_Init(SdlNative.InitVideo) != 0) throw new InvalidOperationException(SdlNative.Error());
    SdlNative.SDL_ShowCursor(0);
    IntPtr window = IntPtr.Zero, renderer = IntPtr.Zero, texture = IntPtr.Zero;
    EditorSession? editor = null;
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
        if (options.Editor is not null)
        {
            if (options.Font is null) throw new ArgumentException("--editor requires --font FILE");
            editor = EditorSession.Load(options.Editor, options.Font);
        }
        SdlNative.SDL_StartTextInput();
        var running = true;
        var paused = false;
        var uartOutput = string.Empty;
        byte mouseButtons = 0;
        UiLogic.UpdateMouse(machine.Memory, width / 2, height / 2, width, height, options.FlipY, mouseButtons);

        while (running)
        {
            while (SdlNative.SDL_PollEvent(out var sdlEvent) != 0)
            {
                switch (sdlEvent.Type)
                {
                    case SdlNative.Quit:
                        running = false;
                        break;
                    case SdlNative.TextInput:
                        var text = UiLogic.ReadTextInput(sdlEvent.Text);
                        log.Write($"text-input length={text.Length}");
                        if (editor is null) UiLogic.EnqueueTextInput(machine.Memory, sdlEvent.Text);
                        else editor.InsertText(text);
                        break;
                    case SdlNative.WindowEvent when sdlEvent.Window.Event == SdlNative.WindowEventSizeChanged:
                        width = Math.Max(1, sdlEvent.Window.Data1);
                        height = Math.Max(1, sdlEvent.Window.Data2);
                        break;
                    case SdlNative.MouseMotion:
                        UiLogic.UpdateMouse(machine.Memory, sdlEvent.Motion.X, sdlEvent.Motion.Y, width, height, options.FlipY,
                            InputMapping.MouseButtonsFromState(sdlEvent.Motion.State));
                        editor?.HandleMouseMotion(sdlEvent.Motion.X, sdlEvent.Motion.Y, width, height, options.FlipY, sdlEvent.Motion.State);
                        break;
                    case SdlNative.MouseButtonDown:
                    case SdlNative.MouseButtonUp:
                        var mask = InputMapping.MouseButtonMask(sdlEvent.Button.Button);
                        if (sdlEvent.Type == SdlNative.MouseButtonDown) mouseButtons |= mask;
                        else mouseButtons &= (byte)~mask;
                        UiLogic.UpdateMouse(machine.Memory, sdlEvent.Button.X, sdlEvent.Button.Y, width, height, options.FlipY, mouseButtons);
                        log.Write($"mouse button={(sdlEvent.Type == SdlNative.MouseButtonDown ? "down" : "up")} "
                            + $"button={sdlEvent.Button.Button} x={sdlEvent.Button.X} y={sdlEvent.Button.Y}");
                        editor?.HandleMouseButton(sdlEvent.Type == SdlNative.MouseButtonDown, sdlEvent.Button.Button,
                            sdlEvent.Button.X, sdlEvent.Button.Y, width, height, options.FlipY);
                        break;
                    case SdlNative.KeyDown:
                    case SdlNative.KeyUp:
                        if (sdlEvent.Key.Repeat != 0) break;
                        var key = sdlEvent.Key.Keysym.Sym;
                        if (sdlEvent.Type == SdlNative.KeyDown && key == 27) running = false;
                        else if (sdlEvent.Type == SdlNative.KeyDown && key == 0x40000042) paused = !paused;
                        else if (sdlEvent.Type == SdlNative.KeyDown && key == 0x40000043) machine.Step();
                        else if (editor is not null && sdlEvent.Type == SdlNative.KeyDown) editor.HandleKey(key, sdlEvent.Key.Keysym.Mod);
                        else
                        {
                            var pressed = sdlEvent.Type == SdlNative.KeyDown;
                            var mapped = UiLogic.EnqueueKeyboard(machine.Memory, key, pressed);
                            log.Write($"key {(pressed ? "down" : "up")} sym=0x{key:x8} mapped={mapped} repeat={sdlEvent.Key.Repeat}");
                        }
                        break;
                }
            }

            if (!paused) machine.Run(options.StepsPerFrame);
            var uart = machine.Memory.TakeUartOutput();
            if (uart.Length != 0) uartOutput += uart;
            var litPixels = editor is null
                ? UiLogic.UpdateFramebuffer(machine.Memory, pixels, options.FlipY)
                : editor.Render(pixels, options.FlipY);
            SdlNative.SDL_SetWindowTitle(window, UiLogic.WindowTitle(machine, paused, uartOutput, litPixels));
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
        SdlNative.SDL_StopTextInput();
        if (renderer != IntPtr.Zero) SdlNative.SDL_DestroyRenderer(renderer);
        if (window != IntPtr.Zero) SdlNative.SDL_DestroyWindow(window);
        SdlNative.SDL_ShowCursor(1);
        SdlNative.SDL_Quit();
    }
}
