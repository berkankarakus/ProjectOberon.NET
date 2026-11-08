using System.Runtime.InteropServices;

namespace ProjectOberon.SdlUi;

internal static partial class SdlNative
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

    public static string Error() => Marshal.PtrToStringAnsi(SDL_GetError()) ?? "SDL error";
}
