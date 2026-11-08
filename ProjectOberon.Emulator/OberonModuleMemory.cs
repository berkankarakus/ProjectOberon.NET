using System.Text;

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
    IReadOnlyList<uint> EntryOffsets);

public sealed class OberonModuleMemoryLoader
{
    private const uint DescriptorSize = 80;
    private const uint ModuleTableRegister = 12;
    private const int DescriptorNameLength = 32;

    private readonly Memory _memory;
    private readonly OberonModuleLoader _modules;
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
        ValidateAlignedAddress(allocationAddress, nameof(allocationAddress));
        ValidateAlignedAddress(moduleTableAddress, nameof(moduleTableAddress));
        _nextAddress = allocationAddress;
        ModuleTableAddress = moduleTableAddress;
    }

    public uint ModuleTableAddress { get; }
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

        ValidateAlignedAddress(binding.DataAddress, nameof(binding.DataAddress));
        ValidateAlignedAddress(binding.CodeAddress, nameof(binding.CodeAddress));
        if (_images.ContainsKey(module.Name))
        {
            throw new InvalidOperationException($"external module is already materialized: {module.Name}");
        }

        _externalBindings[module.Name] = binding;
    }

    public OberonModuleImage Load(string name)
    {
        var module = _modules.Load(name);
        if (_images.TryGetValue(module.Name, out var existing))
        {
            return existing;
        }

        if (module.IsExternal)
        {
            if (!_externalBindings.TryGetValue(module.Name, out var binding))
            {
                throw new OberonModuleLoadException(5,
                    $"external Oberon module requires a host memory binding: {module.Name}");
            }

            var externalImage = AllocateExternal(module, binding);
            _images.Add(module.Name, externalImage);
            return externalImage;
        }

        var imports = new OberonModuleImage[module.Imports.Count];
        for (var index = 0; index < imports.Length; index++)
        {
            imports[index] = Load(module.Imports[index].Name);
        }

        var image = Allocate(module, imports);
        _images.Add(module.Name, image);
        return image;
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
        WriteDescriptor(module, descriptorAddress, binding.DataAddress, binding.CodeAddress,
            importAddress, commandAddress, entryAddress, pointerAddress, allocationSize);
        WriteExternalEntries(entryAddress, binding.EntryOffsets);
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
        var entryAddress = WriteCommandTable(commandAddress, definition.Commands);
        var pointerAddress = entryAddress + checked((uint)definition.Entries.Count * 4);
        var endAddress = pointerAddress + checked((uint)(definition.PointerReferences.Count + 1) * 4);
        var allocationSize = Align(endAddress - descriptorAddress, 0x20);

        EnsureRange(descriptorAddress, allocationSize);
        EnsureRange(ModuleTableAddress + checked((uint)(module.Number + 1) * 4), 4);

        WriteDescriptor(module, descriptorAddress, dataAddress, codeAddress, importAddress,
            commandAddress, entryAddress, pointerAddress, allocationSize);
        WriteBytes(dataAddress, definition.TypeDescriptorPayload);
        WriteZeros(dataAddress + checked((uint)definition.TypeDescriptorPayload.Length), definition.VariableBytes);
        WriteBytes(dataAddress + checked((uint)definition.TypeDescriptorPayload.Length + (uint)definition.VariableBytes),
            definition.StringPayload);
        WriteCode(codeAddress, definition.Code);
        WriteImports(importAddress, imports);
        WriteEntries(entryAddress, definition.Entries);
        WritePointerReferences(pointerAddress, dataAddress, definition.PointerReferences);
        _memory.Write32(ModuleTableAddress + checked((uint)module.Number * 4), dataAddress);

        var image = new OberonModuleImage(module, descriptorAddress, dataAddress, codeAddress, importAddress,
            commandAddress, entryAddress, pointerAddress, codeAddress + checked((uint)definition.BodyOffset * 4),
            allocationSize);
        ApplyFixups(image, imports);

        _memory.Write32(descriptorAddress + 32, _rootDescriptorAddress);
        _rootDescriptorAddress = descriptorAddress;
        _nextAddress = descriptorAddress + allocationSize;
        return image;
    }

    private void WriteDescriptor(
        LoadedOberonModule module,
        uint descriptorAddress,
        uint dataAddress,
        uint codeAddress,
        uint importAddress,
        uint commandAddress,
        uint entryAddress,
        uint pointerAddress,
        uint allocationSize)
    {
        WriteString(descriptorAddress, module.Name, DescriptorNameLength);
        _memory.Write32(descriptorAddress + 32, _rootDescriptorAddress);
        _memory.Write32(descriptorAddress + 36, unchecked((uint)module.Definition.Key));
        _memory.Write32(descriptorAddress + 40, checked((uint)module.Number));
        _memory.Write32(descriptorAddress + 44, allocationSize);
        _memory.Write32(descriptorAddress + 48, 0);
        _memory.Write32(descriptorAddress + 52, dataAddress);
        _memory.Write32(descriptorAddress + 56, codeAddress);
        _memory.Write32(descriptorAddress + 60, importAddress);
        _memory.Write32(descriptorAddress + 64, commandAddress);
        _memory.Write32(descriptorAddress + 68, entryAddress);
        _memory.Write32(descriptorAddress + 72, pointerAddress);
        _memory.Write32(descriptorAddress + 76, 0);
    }

    private uint WriteCommandTable(uint address, IReadOnlyList<RscCommand> commands)
    {
        var cursor = address;
        foreach (var command in commands)
        {
            var commandBytes = Encoding.ASCII.GetBytes(command.Name);
            WriteBytes(cursor, commandBytes);
            cursor += checked((uint)commandBytes.Length);
            _memory.Write8(cursor, 0);
            cursor = Align(cursor + 1, 4);
            _memory.Write32(cursor, unchecked((uint)command.CodeOffset));
            cursor += 4;
        }

        _memory.Write8(cursor, 0);
        return Align(cursor + 1, 4);
    }

    private void WriteCode(uint address, IReadOnlyList<uint> code)
    {
        for (var index = 0; index < code.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), code[index]);
        }
    }

    private void WriteImports(uint address, IReadOnlyList<OberonModuleImage> imports)
    {
        for (var index = 0; index < imports.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), imports[index].DescriptorAddress);
        }
    }

    private void WriteEntries(uint address, IReadOnlyList<int> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), unchecked((uint)entries[index]));
        }
    }

    private void WriteExternalEntries(uint address, IReadOnlyList<uint> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), entries[index]);
        }
    }

    private void WritePointerReferences(uint address, uint dataAddress, IReadOnlyList<int> references)
    {
        for (var index = 0; index < references.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4),
                dataAddress + checked((uint)references[index]));
        }

        _memory.Write32(address + checked((uint)references.Count * 4), 0);
    }

    private void ApplyFixups(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
    {
        ApplyProgramFixups(image, imports);
        ApplyDataFixups(image, imports);
        ApplyTypeFixups(image, imports);
    }

    private void ApplyProgramFixups(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
    {
        var fixup = image.Module.Definition.FixupProgram;
        if (fixup == 0)
        {
            return;
        }

        var address = image.CodeAddress + checked((uint)fixup * 4);
        var codeEnd = image.CodeAddress + checked((uint)image.Module.Definition.CodeWords * 4);
        var visited = new HashSet<uint>();
        while (address != image.CodeAddress)
        {
            EnsureFixupAddress(address, image.CodeAddress, codeEnd, visited);
            var instruction = _memory.Read32(address);
            var importNumber = (int)((instruction >> 20) & 0xf);
            var entryNumber = (int)((instruction >> 12) & 0xff);
            var imported = GetImport(imports, importNumber);
            var destination = imported.CodeAddress + ReadEntry(imported, entryNumber);
            var displacement = ((long)destination - address - 4) / 4;
            var fixedInstruction = 0xf700_0000u | (unchecked((uint)displacement) & 0x00ff_ffffu);
            _memory.Write32(address, fixedInstruction);
            address = PreviousFixup(address, instruction, image.CodeAddress);
        }
    }

    private void ApplyDataFixups(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
    {
        var fixup = image.Module.Definition.FixupData;
        if (fixup == 0)
        {
            return;
        }

        var address = image.CodeAddress + checked((uint)fixup * 4);
        var codeEnd = image.CodeAddress + checked((uint)image.Module.Definition.CodeWords * 4);
        var visited = new HashSet<uint>();
        while (address != image.CodeAddress)
        {
            EnsureFixupAddress(address, image.CodeAddress, codeEnd, visited);
            var instruction = _memory.Read32(address);
            var importNumber = (int)((instruction >> 20) & 0xf);
            var displacement = instruction & 0xfff;
            var moduleNumber = importNumber == 0
                ? image.Module.Number
                : GetImport(imports, importNumber).Module.Number;
            var fixedInstruction = (((instruction >> 24) * 0x10u) + ModuleTableRegister) << 20;
            fixedInstruction |= checked((uint)moduleNumber * 4);
            _memory.Write32(address, fixedInstruction);

            var offsetInstruction = _memory.Read32(address + 4);
            if (importNumber != 0)
            {
                var imported = GetImport(imports, importNumber);
                var entryNumber = (int)(offsetInstruction & 0xff);
                var offset = ReadEntry(imported, entryNumber);
                if (((offsetInstruction >> 8) & 1) != 0)
                {
                    offset += imported.CodeAddress - imported.DataAddress;
                }

                _memory.Write32(address + 4, (offsetInstruction & 0xffff_0000u) | (offset & 0xffffu));
            }

            address = PreviousFixup(address, displacement, image.CodeAddress);
        }
    }

    private void ApplyTypeFixups(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
    {
        var fixup = image.Module.Definition.FixupTypes;
        if (fixup == 0)
        {
            return;
        }

        var address = image.DataAddress + checked((uint)fixup * 4);
        var dataEnd = image.DataAddress + checked((uint)image.Module.Definition.TypeDescriptorPayload.Length);
        var visited = new HashSet<uint>();
        while (address != image.DataAddress)
        {
            EnsureFixupAddress(address, image.DataAddress, dataEnd, visited);
            var instruction = _memory.Read32(address);
            var importNumber = (int)((instruction >> 24) & 0xf);
            var entryNumber = (int)((instruction >> 12) & 0xfff);
            var offset = (uint)entryNumber;
            var target = importNumber == 0
                ? image.DataAddress + offset
                : GetImport(imports, importNumber).DataAddress + ReadEntry(GetImport(imports, importNumber), entryNumber);
            _memory.Write32(address, target);
            address = PreviousFixup(address, instruction, image.DataAddress);
        }
    }

    private static uint PreviousFixup(uint address, uint instruction, uint lowerBound)
    {
        var displacement = instruction & 0xfff;
        if (displacement == 0)
        {
            throw new InvalidDataException("RSC fixup chain has a zero displacement");
        }

        var previous = address - checked(displacement * 4);
        if (previous < lowerBound)
        {
            throw new InvalidDataException("RSC fixup chain points outside its section");
        }

        return previous;
    }

    private static void EnsureFixupAddress(
        uint address,
        uint lowerBound,
        uint upperBound,
        HashSet<uint> visited)
    {
        if (address < lowerBound || address >= upperBound || !visited.Add(address))
        {
            throw new InvalidDataException("invalid RSC fixup chain");
        }
    }

    private static OberonModuleImage GetImport(IReadOnlyList<OberonModuleImage> imports, int importNumber)
    {
        if (importNumber <= 0 || importNumber > imports.Count)
        {
            throw new InvalidDataException($"invalid RSC import number: {importNumber}");
        }

        return imports[importNumber - 1];
    }

    private uint ReadEntry(OberonModuleImage image, int entryNumber)
    {
        if (entryNumber < 0 || entryNumber >= image.Module.Definition.Entries.Count)
        {
            throw new InvalidDataException($"invalid RSC entry number: {entryNumber}");
        }

        return _memory.Read32(image.EntryAddress + checked((uint)entryNumber * 4));
    }

    private void WriteBytes(uint address, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            _memory.Write8(address + checked((uint)index), data[index]);
        }
    }

    private void WriteZeros(uint address, int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"negative RSC variable space: {count}");
        }

        for (var index = 0; index < count; index++)
        {
            _memory.Write8(address + checked((uint)index), 0);
        }
    }

    private void WriteString(uint address, string value, int capacity)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length >= capacity)
        {
            throw new InvalidDataException($"Oberon module name is too long: {value}");
        }

        WriteBytes(address, bytes);
        _memory.Write8(address + checked((uint)bytes.Length), 0);
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
}
