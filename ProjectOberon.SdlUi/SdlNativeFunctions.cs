using System.Runtime.InteropServices;

namespace ProjectOberon.SdlUi;

internal static partial class SdlNative
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Quit();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_ShowCursor(int toggle);
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
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_GetClipboardText();
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] public static extern int SDL_SetClipboardText(string text);
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_free(IntPtr memory);
}
