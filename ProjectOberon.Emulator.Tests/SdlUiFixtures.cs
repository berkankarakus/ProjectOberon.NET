using System.Buffers.Binary;

internal static partial class SdlUiTests
{
    private static byte[] BuildFont()
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
        for (var index = 0; index < 3; index++)
        {
            AddInt16(data, 3);
            AddInt16(data, 0);
            AddInt16(data, 0);
            AddInt16(data, 2);
            AddInt16(data, 2);
        }

        for (var index = 0; index < 3; index++)
        {
            data.Add(1);
            data.Add(2);
        }

        return data.ToArray();
    }

    private static void AddInt16(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)value));
        data.AddRange(bytes.ToArray());
    }
}
