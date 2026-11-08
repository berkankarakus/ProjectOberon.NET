namespace ProjectOberon.Emulator;

public sealed class OberonModuleRuntime
{
    private readonly Memory _memory;
    private readonly Risc5Cpu _cpu;

    public OberonModuleRuntime(
        Memory memory,
        Risc5Cpu cpu,
        OberonModuleLoader modules,
        uint allocationAddress = 0x1000,
        uint moduleTableAddress = 0x100)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
        MemoryLoader = new OberonModuleMemoryLoader(memory, modules, allocationAddress, moduleTableAddress);
    }

    public OberonModuleMemoryLoader MemoryLoader { get; }
    public uint ModuleTableAddress => MemoryLoader.ModuleTableAddress;

    public OberonModuleImage Start(string name, uint? stackPointer = null)
    {
        var image = MemoryLoader.Load(name);
        var stack = stackPointer ?? AlignStack(_memory.RamSize);
        ValidateStack(stack);

        _cpu.SetRegister(12, ModuleTableAddress);
        _cpu.SetRegister(14, stack);
        _cpu.SetProgramCounter(image.BodyAddress);
        return image;
    }

    private static uint AlignStack(int ramSize)
    {
        if (ramSize < 4)
        {
            throw new InvalidOperationException("emulator memory is too small for an Oberon stack");
        }

        return (uint)(ramSize - 4);
    }

    private void ValidateStack(uint stackPointer)
    {
        if ((stackPointer & 3) != 0)
        {
            throw new ArgumentException("stack pointer must be word-aligned", nameof(stackPointer));
        }

        if (stackPointer >= (uint)_memory.RamSize)
        {
            throw new ArgumentOutOfRangeException(nameof(stackPointer));
        }
    }
}
