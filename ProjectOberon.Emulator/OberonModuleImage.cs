namespace ProjectOberon.Emulator;

public sealed class OberonModuleImage
{
    internal OberonModuleImage(
        LoadedOberonModule module,
        uint descriptorAddress,
        uint dataAddress,
        uint codeAddress,
        uint importAddress,
        uint commandAddress,
        uint entryAddress,
        uint pointerAddress,
        uint bodyAddress,
        uint allocationSize)
    {
        Module = module;
        DescriptorAddress = descriptorAddress;
        DataAddress = dataAddress;
        CodeAddress = codeAddress;
        ImportAddress = importAddress;
        CommandAddress = commandAddress;
        EntryAddress = entryAddress;
        PointerAddress = pointerAddress;
        BodyAddress = bodyAddress;
        AllocationSize = allocationSize;
    }

    public LoadedOberonModule Module { get; }
    public uint DescriptorAddress { get; }
    public uint DataAddress { get; }
    public uint CodeAddress { get; }
    public uint ImportAddress { get; }
    public uint CommandAddress { get; }
    public uint EntryAddress { get; }
    public uint PointerAddress { get; }
    public uint BodyAddress { get; }
    public uint AllocationSize { get; }

    public uint? FindCommandAddress(string name)
    {
        var command = Module.FindCommand(name);
        if (command is null)
        {
            return null;
        }

        return CodeAddress + checked((uint)command.CodeOffset);
    }
}

public sealed record OberonExternalModuleBinding(
    string Name,
    uint DataAddress,
    uint CodeAddress,
    IReadOnlyList<uint> EntryOffsets)
{
    public IReadOnlyList<Action<OberonHostCallContext>> EntryHandlers { get; init; } = [];
    public IReadOnlyList<uint> HostCallOffsets { get; init; } = [];
}
