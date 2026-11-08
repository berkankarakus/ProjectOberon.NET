using System.Buffers.Binary;
using System.Text;
using ProjectOberon.Emulator;

internal static class OberonBootSessionTests
{
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
