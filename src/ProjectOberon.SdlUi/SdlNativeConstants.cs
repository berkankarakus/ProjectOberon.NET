namespace ProjectOberon.SdlUi;

internal static partial class SdlNative
{
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
    public const ushort KeymodShift = 0x0003;
    public const ushort KeymodCtrl = 0x00c0;
}
