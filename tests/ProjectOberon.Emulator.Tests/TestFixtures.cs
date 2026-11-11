using ProjectOberon.Emulator;

internal static class TestFixtures
{
    public static void AddString(List<byte> data, string value)
    {
        data.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
        data.Add(0);
    }

    public static void AddInt(List<byte> data, int value)
    {
        data.AddRange(BitConverter.GetBytes(value));
    }

    public static uint Transfer(Memory memory, uint dataAddress, byte value)
    {
        memory.Write32(dataAddress, value);
        return memory.Read32(dataAddress);
    }

    public static void SendCommand(Memory memory, uint dataAddress, byte command, uint argument)
    {
        Transfer(memory, dataAddress, (byte)(0x40 | command));
        Transfer(memory, dataAddress, (byte)(argument >> 24));
        Transfer(memory, dataAddress, (byte)(argument >> 16));
        Transfer(memory, dataAddress, (byte)(argument >> 8));
        Transfer(memory, dataAddress, (byte)argument);
        Transfer(memory, dataAddress, 0xff);
    }

    public static uint MovImmediate(int a, ushort value, bool negative = false) => InstructionBits.Q | ((uint)a << 24) | (negative ? InstructionBits.V : 0) | value;
    public static uint Alu(AluOperation op, int a, int b, int c) => ((uint)a << 24) | ((uint)b << 20) | ((uint)op << 16) | (uint)c;
    public static uint MemoryOp(bool store, bool byteAccess, int a, int b, uint offset) => InstructionBits.P | (store ? InstructionBits.U : 0) | (byteAccess ? InstructionBits.V : 0) | ((uint)a << 24) | ((uint)b << 20) | (offset & InstructionBits.ImmediateMask);
    public static uint BranchRelative(ConditionCode condition, int displacement, bool link = false) => InstructionBits.P | InstructionBits.Q | InstructionBits.U | (link ? InstructionBits.V : 0) | ((uint)condition << 24) | ((uint)displacement & InstructionBits.BranchDisplacementMask);

    public static uint[] RomWith(params uint[] words)
    {
        var rom = new uint[Memory.RomWords];
        words.CopyTo(rom, 0);
        return rom;
    }
}
