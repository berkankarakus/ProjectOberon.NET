using ProjectOberon.Emulator;

internal static partial class OberonModuleTests
{
    public static void Fixups()
    {
        var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start", typePayload: [0x44, 0x33, 0x22, 0x11, 1, 0, 0, 0], code: [0xc700_0000u, 0x0010_0001u, 0, 0x8b10_0003u, 0x0000_0100u], fixupProgram: 1, fixupData: 3, fixupTypes: 1),
            ["B.rsc"] = BuildRsc("B", 200, [], "Run", typePayload: [0, 0, 0, 0], variableBytes: 4, code: [0], entries: [4])
        };
        var moduleLoader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
        var memory = new Memory(64 * 1024);
        var imageLoader = new OberonModuleMemoryLoader(memory, moduleLoader, 0x1000, 0x100);
        var image = imageLoader.Load("A");
        var imported = imageLoader.LoadedImages.Single(candidate => candidate.Module.Name == "B");
        var branchAddress = image.CodeAddress + 4;
        var branchTarget = imported.CodeAddress + (uint)imported.Module.Definition.Entries[0];
        var branchOffset = (uint)(((long)branchTarget - branchAddress - 4) / 4);
        TestAssertions.Equal(0xf700_0000u | (branchOffset & 0x00ff_ffffu), memory.Read32(branchAddress));
        var dataFixup = image.CodeAddress + 12;
        var expectedDataInstruction = (((0x8bu * 0x10u) + 12) << 20) | ((uint)imported.Module.Number * 4);
        TestAssertions.Equal(expectedDataInstruction, memory.Read32(dataFixup));
        var importedOffset = 4u + imported.CodeAddress - imported.DataAddress;
        TestAssertions.Equal(importedOffset, memory.Read32(dataFixup + 4));
        TestAssertions.Equal(image.DataAddress, memory.Read32(image.DataAddress + 4));

        var brokenLoader = new OberonModuleLoader(name => name == "Broken"
            ? BuildRsc("Broken", 300, [], "Start", code: [0, 0], fixupProgram: 1)
            : null);
        var brokenMemoryLoader = new OberonModuleMemoryLoader(new Memory(64 * 1024), brokenLoader);
        TestAssertions.Throws<OberonModuleLoadException>(() => brokenMemoryLoader.Load("Broken"));
    }
}
