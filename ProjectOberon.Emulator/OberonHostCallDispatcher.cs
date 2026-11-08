namespace ProjectOberon.Emulator;

public sealed class OberonHostCallContext
{
    internal OberonHostCallContext(Memory memory, Risc5Cpu cpu)
    {
        Memory = memory;
        Cpu = cpu;
    }

    public Memory Memory { get; }

    public Risc5Cpu Cpu { get; }

    public uint Argument(int register)
    {
        return Cpu.Register(register);
    }

    public byte[] ReadBytes(uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var bytes = new byte[length];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Memory.Read8(address + checked((uint)index));
        }

        return bytes;
    }

    public void WriteBytes(uint address, ReadOnlySpan<byte> bytes)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            Memory.Write8(address + checked((uint)index), bytes[index]);
        }
    }

    public void Return()
    {
        Cpu.SetProgramCounter(Cpu.Register(15));
    }
}

public sealed class OberonHostCallDispatcher
{
    private readonly Dictionary<uint, Action<OberonHostCallContext>> _handlers = [];

    public IReadOnlyCollection<uint> Addresses => _handlers.Keys;

    public void Register(uint address, Action<OberonHostCallContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if ((address & 3) != 0)
        {
            throw new ArgumentException("host call address must be word-aligned", nameof(address));
        }

        if (!_handlers.TryAdd(address & Memory.AddressMask, handler))
        {
            throw new InvalidOperationException($"host call address is already registered: 0x{address:x6}");
        }
    }

    internal bool TryDispatch(Memory memory, Risc5Cpu cpu)
    {
        if (!_handlers.TryGetValue(cpu.PcByte, out var handler))
        {
            return false;
        }

        handler(new OberonHostCallContext(memory, cpu));
        cpu.CountHostCall();
        return true;
    }
}
