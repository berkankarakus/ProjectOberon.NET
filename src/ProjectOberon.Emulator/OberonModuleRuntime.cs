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
        uint moduleTableAddress = 0x20)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
        MemoryLoader = new OberonModuleMemoryLoader(memory, modules, allocationAddress, moduleTableAddress);
    }

    public OberonModuleMemoryLoader MemoryLoader { get; }
    internal Memory Memory => _memory;
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
        var handlerOffsets = binding.HostCallOffsets.Count == 0
            ? binding.EntryOffsets
            : binding.HostCallOffsets;
        if (binding.EntryHandlers.Count != handlerOffsets.Count)
        {
            throw new ArgumentException("external handler count does not match host-call offsets", nameof(binding));
        }

        for (var index = 0; index < handlerOffsets.Count; index++)
        {
            dispatcher.Register(binding.CodeAddress + handlerOffsets[index], binding.EntryHandlers[index]);
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
        var savedRegisters = new uint[16];
        for (var index = 0; index < savedRegisters.Length; index++)
        {
            savedRegisters[index] = _cpu.Register(index);
        }

        var savedStack = savedRegisters[14];
        try
        {
            var initializerStack = savedStack == 0 ? AlignStack(_memory.RamSize) : savedStack;
            ValidateStack(initializerStack);
            for (var index = 0; index < savedRegisters.Length; index++)
            {
                _cpu.SetRegister(index, 0);
            }

            _cpu.SetRegister(12, ModuleTableAddress);
            _cpu.SetRegister(13, image.DataAddress);
            _cpu.SetRegister(14, initializerStack);
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
            for (var index = 0; index < savedRegisters.Length; index++)
            {
                _cpu.SetRegister(index, savedRegisters[index]);
            }

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
        _cpu.SetRegister(13, image.DataAddress);
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

        if (stackPointer <= MemoryLoader.NextAllocationAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(stackPointer),
                "stack pointer overlaps loaded Oberon modules");
        }
    }
}
