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
        var binding = OberonModulesHostBinding.Create(symbol, runtime, loader, 0x3000, 0x5000, machine.HostCalls);
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
        TestAssertions.Equal(descriptor, machine.Memory.Read32(0x3008));
        TestAssertions.Equal(runtime.ModuleTableAddress, machine.Memory.Read32(0x300c));
        TestAssertions.True(machine.Memory.Read32(0x3010) > 0);

        machine.Cpu.SetRegister(0, descriptor);
        machine.Cpu.SetRegister(1, 0x700);
        machine.Cpu.SetProgramCounter(0x5024);
        machine.Step();
        var commandAddress = machine.Cpu.Register(0);
        TestAssertions.Equal(runtime.MemoryLoader.LoadedImages.Single().CodeAddress, commandAddress);

        TestAssertions.Equal(0x200u, machine.Cpu.PcByte);
        TestAssertions.Equal(0x200u, machine.Cpu.Register(15));
        TestAssertions.Equal(3ul, machine.Cpu.Steps);

        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetRegister(1, 0x800);
        machine.Cpu.SetProgramCounter(0x5020);
        machine.Step();
        TestAssertions.Equal(4ul, machine.Cpu.Steps);

        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetProgramCounter(0x5028);
        machine.Step();
        TestAssertions.False(loader.LoadedModules.Any(module => module.Name == "Demo"));
    }

    public static void ReportsLoadErrorsThroughModuleState()
    {
        var symbol = new OberonSymbolModule(
            "Modules",
            1,
            1,
            [new OberonSymbolExport("Load", 1, 0x20)]);
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        using var machine = new Machine();
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        var binding = OberonModulesHostBinding.Create(symbol, runtime, loader, 0x3000, 0x5000, machine.HostCalls);
        runtime.BindExternal(binding, machine.HostCalls);
        WriteCString(machine.Memory, 0x600, "Missing");
        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetRegister(1, 0x800);
        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x5020);

        machine.Step();

        TestAssertions.Equal(0u, machine.Memory.Read32(0x800));
        TestAssertions.Equal(2u, machine.Memory.Read32(0x3014));
        TestAssertions.Equal("Missing", ReadCString(machine.Memory, 0x3018));
        TestAssertions.Equal(0x200u, machine.Cpu.PcByte);
    }

    public static void LimitsNonReturningInitializer()
    {
        var loader = new OberonModuleLoader(_ => BuildRsc(0));
        using var machine = new Machine();
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        var image = runtime.MemoryLoader.Load("Demo");
        machine.Cpu.SetRegister(0, 0x1234);
        machine.Cpu.SetRegister(1, 0x5678);
        machine.Cpu.SetRegister(13, 0x9abc);

        TestAssertions.Throws<OberonModuleLoadException>(
            () => runtime.Initialize(image, machine.HostCalls, maxSteps: 3));
        TestAssertions.Equal(0x1234u, machine.Cpu.Register(0));
        TestAssertions.Equal(0x5678u, machine.Cpu.Register(1));
        TestAssertions.Equal(0x9abcu, machine.Cpu.Register(13));
        TestAssertions.Equal(0u, machine.Cpu.Register(12));
        TestAssertions.Equal(0u, machine.Cpu.Register(14));
    }

    private static byte[] BuildRsc(uint code = 0xc700_000fu)
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
        AddInt(data, unchecked((int)code));
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

    private static string ReadCString(Memory memory, uint address)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var value = memory.Read8(address + (uint)bytes.Count);
            if (value == 0)
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            bytes.Add(value);
        }
    }
}
