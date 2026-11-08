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

    public void BindExternal(OberonExternalModuleBinding binding)
    {
        MemoryLoader.BindExternal(binding);
    }

    public void BindExternal(
        OberonExternalModuleBinding binding,
        OberonHostCallDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        BindExternal(binding);
        if (binding.EntryHandlers.Count != binding.EntryOffsets.Count)
        {
            throw new ArgumentException("external handler count does not match the symbol module", nameof(binding));
        }

        for (var index = 0; index < binding.EntryOffsets.Count; index++)
        {
            dispatcher.Register(binding.CodeAddress + binding.EntryOffsets[index], binding.EntryHandlers[index]);
        }
    }

    public void Initialize(
        OberonModuleImage image,
        OberonHostCallDispatcher dispatcher,
        ulong maxSteps = 100_000)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (maxSteps == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps));
        }

        const uint returnAddress = Memory.RomBase;
        var savedProgramCounter = _cpu.PcByte;
        var savedLink = _cpu.Register(15);
        try
        {
            _cpu.SetRegister(15, returnAddress);
            _cpu.SetProgramCounter(image.BodyAddress);
            for (ulong step = 0; step < maxSteps; step++)
            {
                if (_cpu.PcByte == returnAddress)
                {
                    return;
                }

                if (!dispatcher.TryDispatch(_memory, _cpu))
                {
                    _cpu.Step();
                }
            }

            if (_cpu.PcByte != returnAddress)
            {
                throw new OberonModuleLoadException(7,
                    $"Oberon module initializer exceeded {maxSteps} steps: {image.Module.Name}");
            }
        }
        finally
        {
            _cpu.SetRegister(15, savedLink);
            _cpu.SetProgramCounter(savedProgramCounter);
        }
    }

    public OberonModuleImage Start(string name, uint? stackPointer = null)
    {
        return StartCore(name, commandName: null, stackPointer);
    }

    public OberonModuleImage StartCommand(string name, string commandName, uint? stackPointer = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandName);
        return StartCore(name, commandName, stackPointer);
    }

    private OberonModuleImage StartCore(string name, string? commandName, uint? stackPointer)
    {
        var image = MemoryLoader.Load(name);
        var stack = stackPointer ?? AlignStack(_memory.RamSize);
        ValidateStack(stack);

        var entryAddress = image.BodyAddress;
        if (commandName is not null)
        {
            entryAddress = image.FindCommandAddress(commandName)
                ?? throw new OberonModuleLoadException(2,
                    $"Oberon command is not available: {image.Module.Name}.{commandName}");
        }

        _cpu.SetRegister(12, ModuleTableAddress);
        _cpu.SetRegister(14, stack);
        _cpu.SetProgramCounter(entryAddress);
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
