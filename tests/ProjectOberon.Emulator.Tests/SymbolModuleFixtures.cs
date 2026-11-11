using System.Buffers.Binary;

internal static partial class SymbolModuleTests
{
    private static byte[] BuildSmb(Action<SymbolWriter> writeExports)
    {
        var writer = new SymbolWriter();
        writer.AddInt(0);
        writer.AddInt(0x1234_5678);
        writer.AddString("Kernel");
        writer.AddByte(1);
        writeExports(writer);
        writer.AddByte(0);
        return writer.ToArray();
    }

    private sealed class SymbolWriter
    {
        private readonly List<byte> _data = [];

        public void AddByte(byte value) => _data.Add(value);

        public void AddInt(int value)
        {
            var bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            _data.AddRange(bytes);
        }

        public void AddString(string value)
        {
            _data.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
            _data.Add(0);
        }

        public byte[] ToArray() => _data.ToArray();
    }
}
