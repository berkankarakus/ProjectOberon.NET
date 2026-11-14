namespace ProjectOberon.Emulator;

public sealed class OberonModuleMemoryLoader
{
    private const uint DescriptorSize = 80;

    private readonly Memory _memory;
    private readonly OberonModuleLoader _modules;
    private readonly OberonModuleMemoryWriter _writer;
    private readonly OberonModuleFixups _fixups;
    private readonly Dictionary<string, OberonModuleImage> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OberonExternalModuleBinding> _externalBindings = new(StringComparer.Ordinal);
    private uint _nextAddress;
    private uint _rootDescriptorAddress;

    public OberonModuleMemoryLoader(
        Memory memory,
        OberonModuleLoader modules,
        uint allocationAddress = 0x1000,
        uint moduleTableAddress = 0x100)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
        _writer = new OberonModuleMemoryWriter(memory);
        _fixups = new OberonModuleFixups(memory);
        ValidateAlignedAddress(allocationAddress, nameof(allocationAddress));
        ValidateAlignedAddress(moduleTableAddress, nameof(moduleTableAddress));
        _nextAddress = allocationAddress;
        ModuleTableAddress = moduleTableAddress;
    }

    public uint ModuleTableAddress { get; }
    public uint NextAllocationAddress => _nextAddress;
    public IReadOnlyCollection<OberonModuleImage> LoadedImages => _images.Values;
    public uint RootDescriptorAddress => _rootDescriptorAddress;

    public void BindExternal(OberonExternalModuleBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var module = _modules.Load(binding.Name);
        if (!module.IsExternal)
        {
            throw new ArgumentException($"module is not external: {module.Name}", nameof(binding));
        }

        if (binding.EntryOffsets.Count != module.Definition.Entries.Count)
        {
            throw new ArgumentException("external entry count does not match the symbol module", nameof(binding));
        }

        ValidateWordAlignedOffsets(binding.EntryOffsets, nameof(binding.EntryOffsets));

        ValidateAlignedAddress(binding.DataAddress, nameof(binding.DataAddress));
        ValidateAlignedAddress(binding.CodeAddress, nameof(binding.CodeAddress));
        if (_images.ContainsKey(module.Name))
        {
            throw new InvalidOperationException($"external module is already materialized: {module.Name}");
        }

        _externalBindings[module.Name] = binding;
    }

    internal bool Unload(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!_images.Remove(name, out var image))
        {
            return false;
        }

        var previousRoot = _memory.Read32(image.DescriptorAddress + 32);
        if (_rootDescriptorAddress == image.DescriptorAddress)
        {
            _rootDescriptorAddress = previousRoot;
        }
        else
        {
            foreach (var successor in _images.Values)
            {
                if (_memory.Read32(successor.DescriptorAddress + 32) == image.DescriptorAddress)
                {
                    _memory.Write32(successor.DescriptorAddress + 32, previousRoot);
                    break;
                }
            }
        }

        _memory.Write32(ModuleTableAddress + checked((uint)image.Module.Number * 4), 0);
        if (_nextAddress == image.DescriptorAddress + image.AllocationSize)
        {
            _nextAddress = image.DescriptorAddress;
        }

        return true;
    }

    public OberonModuleImage Load(string name)
    {
        var existingNames = _images.Keys.ToHashSet(StringComparer.Ordinal);
        var savedNextAddress = _nextAddress;
        var savedRootDescriptorAddress = _rootDescriptorAddress;
        var module = _modules.Load(name);
        if (_images.TryGetValue(module.Name, out var existing))
        {
            return existing;
        }

        try
        {
            var image = module.IsExternal
                ? LoadExternal(module)
                : LoadManaged(module);
            _images.Add(module.Name, image);
            return image;
        }
        catch
        {
            RollbackMaterialization(existingNames, savedNextAddress, savedRootDescriptorAddress, module.Number);
            throw;
        }
    }

    private OberonModuleImage LoadExternal(LoadedOberonModule module)
    {
        if (!_externalBindings.TryGetValue(module.Name, out var binding))
        {
            throw new OberonModuleLoadException(5,
                $"external Oberon module requires a host memory binding: {module.Name}");
        }

        return AllocateExternal(module, binding);
    }

    private OberonModuleImage LoadManaged(LoadedOberonModule module)
    {
        var imports = new OberonModuleImage[module.Imports.Count];
        for (var index = 0; index < imports.Length; index++)
        {
            imports[index] = Load(module.Imports[index].Name);
        }

        try
        {
            return Allocate(module, imports);
        }
        catch (OberonModuleLoadException)
        {
            throw;
        }
        catch (InvalidDataException error)
        {
            throw new OberonModuleLoadException(5,
                $"could not materialize Oberon module {module.Name}: {error.Message}");
        }
    }

    private OberonModuleImage AllocateExternal(
        LoadedOberonModule module,
        OberonExternalModuleBinding binding)
    {
        var descriptorAddress = _nextAddress;
        var importAddress = descriptorAddress + DescriptorSize;
        var commandAddress = importAddress;
        var entryAddress = commandAddress;
        var pointerAddress = entryAddress + checked((uint)binding.EntryOffsets.Count * 4);
        var endAddress = pointerAddress + 4;
        var allocationSize = Align(endAddress - descriptorAddress, 0x20);

        EnsureRange(descriptorAddress, allocationSize);
        EnsureRange(ModuleTableAddress + checked((uint)(module.Number + 1) * 4), 4);
        _writer.WriteDescriptor(module, descriptorAddress, binding.DataAddress, binding.CodeAddress,
            importAddress, commandAddress, entryAddress, pointerAddress, allocationSize, _rootDescriptorAddress);
        _writer.WriteExternalEntries(entryAddress, binding.EntryOffsets);
        _memory.Write32(ModuleTableAddress + checked((uint)module.Number * 4), binding.DataAddress);

        var image = new OberonModuleImage(module, descriptorAddress, binding.DataAddress, binding.CodeAddress,
            importAddress, commandAddress, entryAddress, pointerAddress, binding.CodeAddress, allocationSize);
        _memory.Write32(descriptorAddress + 32, _rootDescriptorAddress);
        _rootDescriptorAddress = descriptorAddress;
        _nextAddress = descriptorAddress + allocationSize;
        return image;
    }

    private OberonModuleImage Allocate(LoadedOberonModule module, IReadOnlyList<OberonModuleImage> imports)
    {
        var definition = module.Definition;
        var descriptorAddress = _nextAddress;
        var dataAddress = descriptorAddress + DescriptorSize;
        var codeAddress = dataAddress + checked((uint)definition.TypeDescriptorPayload.Length);
        codeAddress += checked((uint)definition.VariableBytes);
        codeAddress += checked((uint)definition.StringPayload.Length);
        var importAddress = codeAddress + checked((uint)definition.CodeWords * 4);
        var commandAddress = importAddress + checked((uint)imports.Count * 4);
        var entryAddress = _writer.WriteCommandTable(commandAddress, definition.Commands);
        var pointerAddress = entryAddress + checked((uint)definition.Entries.Count * 4);
        var endAddress = pointerAddress + checked((uint)(definition.PointerReferences.Count + 1) * 4);
        var allocationSize = Align(endAddress - descriptorAddress, 0x20);

        EnsureRange(descriptorAddress, allocationSize);
        EnsureRange(ModuleTableAddress + checked((uint)(module.Number + 1) * 4), 4);

        _writer.WriteDescriptor(module, descriptorAddress, dataAddress, codeAddress, importAddress,
            commandAddress, entryAddress, pointerAddress, allocationSize, _rootDescriptorAddress);
        _writer.WriteBytes(dataAddress, definition.TypeDescriptorPayload);
        _writer.WriteZeros(dataAddress + checked((uint)definition.TypeDescriptorPayload.Length), definition.VariableBytes);
        _writer.WriteBytes(dataAddress + checked((uint)definition.TypeDescriptorPayload.Length + (uint)definition.VariableBytes),
            definition.StringPayload);
        _writer.WriteCode(codeAddress, definition.Code);
        _writer.WriteImports(importAddress, imports);
        _writer.WriteEntries(entryAddress, definition.Entries);
        _writer.WritePointerReferences(pointerAddress, dataAddress, definition.PointerReferences);
        _memory.Write32(ModuleTableAddress + checked((uint)module.Number * 4), dataAddress);

        var image = new OberonModuleImage(module, descriptorAddress, dataAddress, codeAddress, importAddress,
            commandAddress, entryAddress, pointerAddress, codeAddress + checked((uint)definition.BodyOffset),
            allocationSize);
        _fixups.Apply(image, imports);

        _memory.Write32(descriptorAddress + 32, _rootDescriptorAddress);
        _rootDescriptorAddress = descriptorAddress;
        _nextAddress = descriptorAddress + allocationSize;
        return image;
    }

    private static uint Align(uint address, uint alignment)
    {
        return checked((address + alignment - 1) / alignment * alignment);
    }

    private static void ValidateAlignedAddress(uint address, string parameterName)
    {
        if ((address & 3) != 0)
        {
            throw new ArgumentException("address must be word-aligned", parameterName);
        }
    }

    private void EnsureRange(uint address, uint length)
    {
        if (address > (uint)_memory.RamSize || length > (uint)_memory.RamSize - address)
        {
            throw new OberonModuleLoadException(7, "not enough emulator memory for Oberon module");
        }
    }

    private void RollbackMaterialization(
        IReadOnlySet<string> existingNames,
        uint savedNextAddress,
        uint savedRootDescriptorAddress,
        int failedModuleNumber)
    {
        foreach (var image in _images.Values
                     .Where(image => !existingNames.Contains(image.Module.Name))
                     .ToArray())
        {
            _images.Remove(image.Module.Name);
            _memory.Write32(ModuleTableAddress + checked((uint)image.Module.Number * 4), 0);
        }

        _memory.Write32(ModuleTableAddress + checked((uint)failedModuleNumber * 4), 0);
        _nextAddress = savedNextAddress;
        _rootDescriptorAddress = savedRootDescriptorAddress;
    }

    private static void ValidateWordAlignedOffsets(IReadOnlyList<uint> offsets, string parameterName)
    {
        for (var index = 0; index < offsets.Count; index++)
        {
            if ((offsets[index] & 3) != 0)
            {
                throw new ArgumentException("entry offsets must be word-aligned", parameterName);
            }
        }
    }
}
