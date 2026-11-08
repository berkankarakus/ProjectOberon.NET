using System.Buffers.Binary;
using ProjectOberon.Emulator;

internal static partial class OberonTextFrameViewerTests
{
    private static OberonViewerSystem CreateSystem()
    {
        var system = new OberonViewerSystem(200, 200);
        system.AddTrack(0, 200);
        return system;
    }

    private static OberonFont CreateFont()
    {
        var data = new List<byte> { 0xdb, 0, 0x53, 0 };
        AddInt16(data, 8);
        AddInt16(data, 0);
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 7);
        AddInt16(data, 1);
        AddInt16(data, 'A');
        AddInt16(data, 'C');
        AddGlyph(data);
        AddGlyph(data);
        return OberonFont.Parse(data.ToArray());
    }

    private static void AddGlyph(List<byte> data)
    {
        AddInt16(data, 3);
        AddInt16(data, 0);
        AddInt16(data, 0);
        AddInt16(data, 2);
        AddInt16(data, 2);
        data.Add(1);
        data.Add(2);
    }

    private static void AddInt16(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)value));
        data.AddRange(bytes.ToArray());
    }
}
