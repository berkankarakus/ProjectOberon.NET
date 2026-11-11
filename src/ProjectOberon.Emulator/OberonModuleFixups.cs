namespace ProjectOberon.Emulator;

internal sealed class OberonModuleFixups
{
    private const uint ModuleTableRegister = 12;
    private readonly Memory _memory;

    public OberonModuleFixups(Memory memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public void Apply(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
    {
        ApplyProgram(image, imports);
        ApplyData(image, imports);
        ApplyTypes(image, imports);
    }

    private void ApplyProgram(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
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

    private void ApplyData(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
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

    private void ApplyTypes(OberonModuleImage image, IReadOnlyList<OberonModuleImage> imports)
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
}
