using System.Text;

namespace ProjectOberon.Emulator;

internal sealed class OberonModuleMemoryWriter
{
    private readonly Memory _memory;

    public OberonModuleMemoryWriter(Memory memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public void WriteDescriptor(
        LoadedOberonModule module,
        uint descriptorAddress,
        uint dataAddress,
        uint codeAddress,
        uint importAddress,
        uint commandAddress,
        uint entryAddress,
        uint pointerAddress,
        uint allocationSize,
        uint rootDescriptorAddress)
    {
        WriteString(descriptorAddress, module.Name, 32);
        _memory.Write32(descriptorAddress + 32, rootDescriptorAddress);
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

    public uint WriteCommandTable(uint address, IReadOnlyList<RscCommand> commands)
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

    public void WriteCode(uint address, IReadOnlyList<uint> code)
    {
        for (var index = 0; index < code.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), code[index]);
        }
    }

    public void WriteImports(uint address, IReadOnlyList<OberonModuleImage> imports)
    {
        for (var index = 0; index < imports.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), imports[index].DescriptorAddress);
        }
    }

    public void WriteEntries(uint address, IReadOnlyList<int> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), unchecked((uint)entries[index]));
        }
    }

    public void WriteExternalEntries(uint address, IReadOnlyList<uint> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4), entries[index]);
        }
    }

    public void WritePointerReferences(uint address, uint dataAddress, IReadOnlyList<int> references)
    {
        for (var index = 0; index < references.Count; index++)
        {
            _memory.Write32(address + checked((uint)index * 4),
                dataAddress + checked((uint)references[index]));
        }

        _memory.Write32(address + checked((uint)references.Count * 4), 0);
    }

    public void WriteBytes(uint address, ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            _memory.Write8(address + checked((uint)index), data[index]);
        }
    }

    public void WriteZeros(uint address, int count)
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
}
