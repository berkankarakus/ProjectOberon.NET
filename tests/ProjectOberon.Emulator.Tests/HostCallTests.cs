using ProjectOberon.Emulator;

internal static class HostCallTests
{
    public static void DispatchesRegisteredHostCalls()
    {
        using var machine = new Machine();
        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x100);
        machine.HostCalls.Register(0x100, context =>
        {
            TestAssertions.Equal(7u, context.Argument(0));
            context.Cpu.SetRegister(1, 42);
            context.Return();
        });
        machine.Cpu.SetRegister(0, 7);

        machine.Step();

        TestAssertions.Equal(42u, machine.Cpu.Register(1));
        TestAssertions.Equal(0x200u, machine.Cpu.PcByte);
        TestAssertions.Equal(1ul, machine.Cpu.Steps);
    }

    public static void BindsExternalEntryHandlers()
    {
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
            [new OberonSymbolExport("clock", 1, 0)]);
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var memory = new Memory();
        var runtime = new OberonModuleRuntime(memory, new Risc5Cpu(memory), loader);
        var called = false;
        var dispatcher = new OberonHostCallDispatcher();
        var binding = new OberonExternalModuleBinding("Kernel", 0x3000, 0x4000, [0x20])
        {
            EntryHandlers = [context =>
            {
                called = true;
                context.Return();
            }]
        };

        runtime.BindExternal(binding, dispatcher);
        TestAssertions.False(called);
        TestAssertions.True(dispatcher.Addresses.Contains(0x4020u));

        var invalidBinding = binding with { HostCallOffsets = [2] };
        TestAssertions.Throws<ArgumentException>(() => runtime.BindExternal(invalidBinding, dispatcher));
    }

    public static void ValidatesHostCallContextArguments()
    {
        using var machine = new Machine();
        machine.HostCalls.Register(0x100, context =>
        {
            TestAssertions.Throws<ArgumentOutOfRangeException>(() => context.Argument(-1));
            TestAssertions.Throws<ArgumentOutOfRangeException>(() => context.Argument(16));
            context.Return();
        });
        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x100);

        machine.Step();
    }

    public static void ValidatesHostCallStrings()
    {
        using var machine = new Machine();
        machine.HostCalls.Register(0x100, context =>
        {
            context.WriteCString(0x600, "ç");
            TestAssertions.Equal("ç", context.ReadCString(0x600));
            TestAssertions.Throws<InvalidDataException>(() => context.ReadCString(0x600, 1));
            TestAssertions.Throws<InvalidDataException>(() => context.WriteCString(0x600, "123", 3));
            TestAssertions.Throws<InvalidDataException>(() => context.WriteCString(0x600, "€"));
            TestAssertions.Throws<InvalidDataException>(() => context.WriteCString(0x600, "a\0b"));
            TestAssertions.Throws<ArgumentOutOfRangeException>(() => context.WriteCString(0x600, "x", 0));
            context.Return();
        });
        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x100);

        machine.Step();
    }

    public static void ValidatesHostCallRegistration()
    {
        var dispatcher = new OberonHostCallDispatcher();
        Action<OberonHostCallContext> handler = _ => { };
        dispatcher.Register(0x100, handler);

        TestAssertions.Throws<InvalidOperationException>(() => dispatcher.Register(0x100, handler));
        TestAssertions.Throws<InvalidOperationException>(() => dispatcher.Register(
            Memory.AddressMask + 0x101, handler));
        TestAssertions.Throws<ArgumentException>(() => dispatcher.Register(0x102, handler));
    }
}
