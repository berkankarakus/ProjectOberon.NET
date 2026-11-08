using System.Buffers.Binary;
using System.Text;
using ProjectOberon.Emulator;

internal static class ModulesHostBindingTests
{
    public static void DispatchesLoadAndCommandServices()
    {
        var symbol = new OberonSymbolModule(
            "Modules",
            1,
            1,
            [
                new OberonSymbolExport("Load", 1, 0x20),
                new OberonSymbolExport("ThisCommand", 1, 0x24),
                new OberonSymbolExport("Free", 1, 0x28),
                new OberonSymbolExport("Init", 1, 0x2c)
            ]);
        var loader = new OberonModuleLoader(name => name == "Demo" ? BuildRsc() : null);
        loader.RegisterExternal(symbol);
        using var machine = new Machine();
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        var binding = OberonModulesHostBinding.Create(symbol, runtime, loader, 0x3000, 0x5000);
        runtime.BindExternal(binding, machine.HostCalls);
        WriteCString(machine.Memory, 0x600, "Demo");
        WriteCString(machine.Memory, 0x700, "Start");
        machine.Cpu.SetRegister(15, 0x200);

        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetRegister(1, 0x800);
        machine.Cpu.SetProgramCounter(0x5020);
        machine.Step();

        var descriptor = machine.Memory.Read32(0x800);
        TestAssertions.True(descriptor != 0);
        TestAssertions.Equal(1, runtime.MemoryLoader.LoadedImages.Count);

        machine.Cpu.SetRegister(0, descriptor);
        machine.Cpu.SetRegister(1, 0x700);
        machine.Cpu.SetProgramCounter(0x5024);
        machine.Step();
        var commandAddress = machine.Cpu.Register(0);
        TestAssertions.Equal(runtime.MemoryLoader.LoadedImages.Single().CodeAddress, commandAddress);

        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetProgramCounter(0x5028);
        machine.Step();
        TestAssertions.False(loader.LoadedModules.Any(module => module.Name == "Demo"));
    }

    private static byte[] BuildRsc()
    {
        var data = new List<byte>();
        AddString(data, "Demo");
        AddInt(data, 42);
        data.Add(1);
        AddInt(data, 0);
        AddString(data, string.Empty);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 1);
        AddInt(data, unchecked((int)0xc700_0000u));
        AddString(data, "Start");
        AddInt(data, 0);
        AddString(data, string.Empty);
        AddInt(data, 1);
        AddInt(data, 0);
        AddInt(data, -1);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 0);
        data.Add((byte)'O');
        return data.ToArray();
    }

    private static void AddString(List<byte> data, string value)
    {
        data.AddRange(Encoding.ASCII.GetBytes(value));
        data.Add(0);
    }

    private static void AddInt(List<byte> data, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        data.AddRange(bytes.ToArray());
    }

    private static void WriteCString(Memory memory, uint address, string value)
    {
        var bytes = Encoding.Latin1.GetBytes(value);
        for (var index = 0; index < bytes.Length; index++) memory.Write8(address + (uint)index, bytes[index]);
        memory.Write8(address + (uint)bytes.Length, 0);
    }
}
