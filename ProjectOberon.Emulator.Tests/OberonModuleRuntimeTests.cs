using ProjectOberon.Emulator;

internal static partial class OberonModuleTests
{
    public static void MemoryImage()
    {
        var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start", code: [0xc700_0010u]),
            ["B.rsc"] = BuildRsc("B", 200, [], "Run", typePayload: [0x44, 0x33, 0x22, 0x11], variableBytes: 4, stringPayload: [(byte)'x', 0, 0, 0], code: [0xc700_0020u])
        };
        var moduleLoader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, moduleLoader, 0x1000, 0x100);
        var image = runtime.Start("A");
        var imported = runtime.MemoryLoader.LoadedImages.Single(candidate => candidate.Module.Name == "B");
        TestAssertions.Equal(imported.DescriptorAddress, memory.Read32(image.ImportAddress));
        TestAssertions.Equal(imported.DataAddress, memory.Read32(0x100u + (uint)imported.Module.Number * 4));
        TestAssertions.Equal(image.DataAddress, memory.Read32(0x100u + (uint)image.Module.Number * 4));
        TestAssertions.Equal(0x11_22_33_44u, memory.Read32(imported.DataAddress));
        TestAssertions.Equal(0u, memory.Read32(imported.DataAddress + 4));
        TestAssertions.Equal((uint)'x', memory.Read32(imported.DataAddress + 8));
        TestAssertions.Equal(0xc700_0020u, memory.Read32(imported.CodeAddress));
        TestAssertions.Equal(imported.CodeAddress, imported.FindCommandAddress("Run")!.Value);
        TestAssertions.Equal(image.CodeAddress, image.BodyAddress);
        TestAssertions.Equal(0x100u, cpu.Register(12));
        TestAssertions.Equal((uint)memory.RamSize - 4, cpu.Register(14));
        TestAssertions.Equal(image.BodyAddress, cpu.PcByte);

        var commandMemory = new Memory(64 * 1024);
        var commandCpu = new Risc5Cpu(commandMemory);
        var commandRuntime = new OberonModuleRuntime(commandMemory, commandCpu, new OberonModuleLoader(
            name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null), 0x1000, 0x100);
        var commandImage = commandRuntime.StartCommand("A", "Start");
        TestAssertions.Equal(commandImage.CodeAddress, commandCpu.PcByte);
        TestAssertions.Throws<OberonModuleLoadException>(() => commandRuntime.StartCommand("A", "Missing"));
    }
}
