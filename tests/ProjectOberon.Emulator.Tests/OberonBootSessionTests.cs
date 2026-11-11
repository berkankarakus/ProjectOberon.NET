using System.Buffers.Binary;
using System.Text;
using ProjectOberon.Emulator;

internal static class OberonBootSessionTests
{
    public static void BootConsumesKeyboardInput()
    {
        using var machine = new Machine();
        var diskPath = Environment.GetEnvironmentVariable("PROJECT_OBERON_DISK");
        machine.OpenDisk(
            string.IsNullOrWhiteSpace(diskPath) ? "RISC-work.img" : diskPath,
            writable: false);
        var boot = OberonBootSession.Create(machine);
        boot.Boot("Oberon");
        machine.Memory.EnqueueKeyboard(0x1c);
        machine.Run(3_000_000);
        var status = machine.Memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus);
        TestAssertions.Equal(0u, status & (1u << 28));
    }

    public static void InitializesAndStartsModule()
    {
        var loader = new OberonModuleLoader(name => name == "Demo" ? BuildRsc() : null);
        loader.RegisterExternal(new OberonSymbolModule(
            "Modules", 1, 1,
            [new OberonSymbolExport("Load", 1, 0x20)]));
        TestAssertions.True(loader.TryGetExternalSymbol("Modules", out var symbol));
        using var machine = new Machine();
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        var binding = OberonModulesHostBinding.Create(
            symbol, runtime, loader,
            OberonCoreHostBinding.ModulesDataAddress,
            OberonCoreHostBinding.ModulesCodeAddress,
            machine.HostCalls,
            entryOffsetScale: 4);
        runtime.BindExternal(binding, machine.HostCalls);
        var boot = OberonBootSession.Create(machine, loader, runtime);

        var descriptor = boot.Initialize("Demo");
        var image = boot.Start("Demo");

        TestAssertions.True(descriptor != 0);
        TestAssertions.Equal(descriptor, image.DescriptorAddress);
        TestAssertions.Equal(image.BodyAddress, machine.Cpu.PcByte);
        TestAssertions.Throws<ArgumentException>(() => boot.Initialize("Démö"));
        TestAssertions.Throws<ArgumentException>(() => boot.Initialize(new string('x', 32)));
    }

    public static void BootsDependenciesBeforeRoot()
    {
        var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["Root"] = BuildRsc("Root", [("Dependency", 2)], key: 1),
            ["Dependency"] = BuildRsc("Dependency", key: 2)
        };
        var loader = new OberonModuleLoader(name => modules.GetValueOrDefault(name));
        loader.RegisterExternal(new OberonSymbolModule(
            "Modules", 1, 1,
            [new OberonSymbolExport("Load", 1, 0x20)]));
        TestAssertions.True(loader.TryGetExternalSymbol("Modules", out var symbol));
        using var machine = new Machine();
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        var binding = OberonModulesHostBinding.Create(
            symbol, runtime, loader,
            OberonCoreHostBinding.ModulesDataAddress,
            OberonCoreHostBinding.ModulesCodeAddress,
            machine.HostCalls,
            entryOffsetScale: 4);
        runtime.BindExternal(binding, machine.HostCalls);
        var boot = OberonBootSession.Create(machine, loader, runtime);

        var image = boot.Boot("Root");

        TestAssertions.Equal(image.BodyAddress, machine.Cpu.PcByte);
        TestAssertions.True(machine.Cpu.Steps > 0);
        TestAssertions.True(runtime.MemoryLoader.LoadedImages.Any(candidate => candidate.Module.Name == "Dependency"));
    }

    private static byte[] BuildRsc(
        string name = "Demo",
        (string Name, int Key)[]? imports = null,
        int key = 42)
    {
        imports ??= [];
        var data = new List<byte>();
        AddString(data, name);
        AddInt(data, key);
        data.Add(1);
        AddInt(data, 0);
        foreach (var import in imports)
        {
            AddString(data, import.Name);
            AddInt(data, import.Key);
        }
        AddString(data, string.Empty);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 0);
        AddInt(data, 1);
        AddInt(data, unchecked((int)0xc700_000fu));
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
}
