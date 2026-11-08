using ProjectOberon.Emulator;

internal static class KernelHostBindingTests
{
    public static void DispatchesClockAndSectorMapServices()
    {
        var memory = new Memory();
        var kernel = new OberonKernel(memory);
        kernel.SetClock(1234);
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
            [
                new OberonSymbolExport("Clock", 1, 0x20),
                new OberonSymbolExport("SetClock", 1, 0x24),
                new OberonSymbolExport("Time", 1, 0x28),
                new OberonSymbolExport("MarkSector", 1, 0x2c)
            ]);
        var binding = OberonKernelHostBinding.Create(symbol, kernel, 0x3000, 0x4000);
        var machine = new Machine();
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);

        runtime.BindExternal(binding, machine.HostCalls);

        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x4020);
        machine.Step();
        TestAssertions.Equal(1234u, machine.Cpu.Register(0));
        TestAssertions.Equal(0x200u, machine.Cpu.PcByte);

        machine.Cpu.SetRegister(0, 4321);
        machine.Cpu.SetProgramCounter(0x4024);
        machine.Step();
        TestAssertions.Equal(4321, kernel.Clock);

        machine.Cpu.SetRegister(0, 29);
        machine.Cpu.SetProgramCounter(0x402c);
        machine.Step();
        TestAssertions.Equal(1, kernel.AllocatedSectors);
    }

    public static void RejectsUnsupportedKernelProcedure()
    {
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
            [new OberonSymbolExport("New", 1, 0x20)]);
        var binding = OberonKernelHostBinding.Create(symbol, new OberonKernel(new Memory()), 0x3000, 0x4000);
        var machine = new Machine();
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        runtime.BindExternal(binding, machine.HostCalls);
        machine.Cpu.SetProgramCounter(0x4020);

        TestAssertions.Throws<OberonHostCallException>(() => machine.Step());
    }
}
