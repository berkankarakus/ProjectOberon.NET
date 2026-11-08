using System.Runtime.InteropServices;

namespace ProjectOberon.SdlUi;

internal static partial class SdlNative
{
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
}
