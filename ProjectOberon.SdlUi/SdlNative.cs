using System.Runtime.InteropServices;

namespace ProjectOberon.SdlUi;

internal static class SdlNative
{
    static SdlNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(SdlNative).Assembly, static (libraryName, _, searchPath) =>
        {
            if (!string.Equals(libraryName, "SDL2", StringComparison.Ordinal)) return IntPtr.Zero;

            foreach (var candidate in RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                         ? new[] { "/opt/homebrew/lib/libSDL2.dylib", "/usr/local/lib/libSDL2.dylib", "libSDL2.dylib" }
                         : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                             ? new[] { "SDL2.dll" }
                             : new[] { "libSDL2.so", "libSDL2-2.0.so.0" })
            {
                if (NativeLibrary.TryLoad(candidate, typeof(SdlNative).Assembly, searchPath, out var handle)) return handle;
            }

            return IntPtr.Zero;
        });
    }

    public const uint InitVideo = 0x20;
    public const uint Quit = 0x100;
    public const uint WindowEvent = 0x200;
    public const uint KeyDown = 0x300;
    public const uint KeyUp = 0x301;
    public const uint TextInput = 0x303;
    public const uint MouseMotion = 0x400;
    public const uint MouseButtonDown = 0x401;
    public const uint MouseButtonUp = 0x402;
    public const byte WindowEventSizeChanged = 0x05;
    public const uint RendererAccelerated = 0x02;
    public const uint RendererPresentVsync = 0x04;
    public const uint WindowResizable = 0x20;
    public const uint TextureAccessStreaming = 1;
    public const uint PixelFormatArgb8888 = 0x16362004;
    public const int WindowPositionCentered = unchecked((int)0x2fff0000);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeySym { public int Scancode; public int Sym; public ushort Mod; public uint Unused; }
    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardEvent { public uint Type, Timestamp, WindowId; public byte State, Repeat; public ushort Padding; public KeySym Keysym; }
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowEventData { public uint Type, Timestamp, WindowId; public byte Event; public byte Pad1, Pad2, Pad3; public int Data1, Data2; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MouseMotionEvent { public uint Type, Timestamp, WindowId, State; public int X, Y, Xrel, Yrel; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MouseButtonEvent { public uint Type, Timestamp, WindowId; public byte Button, State, Clicks, Padding; public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TextInputEvent
    {
        public uint Type, Timestamp, WindowId;
        public fixed byte Text[32];
    }
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public unsafe struct Event
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(0)] public KeyboardEvent Key;
        [FieldOffset(0)] public WindowEventData Window;
        [FieldOffset(0)] public MouseMotionEvent Motion;
        [FieldOffset(0)] public MouseButtonEvent Button;
        [FieldOffset(0)] public TextInputEvent Text;
    }

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Quit();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_StartTextInput();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_StopTextInput();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GetError();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern IntPtr SDL_CreateWindow(string title, int x, int y, int w, int h, uint flags);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateRenderer(IntPtr window, int index, uint flags);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int w, int h);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, uint[] pixels, int pitch);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_SetWindowTitle(IntPtr window, string title);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_PollEvent(out Event sdlEvent);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr source, IntPtr destination);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_RenderPresent(IntPtr renderer);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Delay(uint milliseconds);

    public static string Error() => Marshal.PtrToStringAnsi(SDL_GetError()) ?? "SDL error";
}
