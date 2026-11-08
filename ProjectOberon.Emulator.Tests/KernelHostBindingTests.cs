using ProjectOberon.Emulator;

internal static class KernelHostBindingTests
{
    public static void DispatchesClockAndSectorMapServices()
    {
        var machine = new Machine();
        var kernel = new OberonKernel(machine.Memory);
        kernel.SetClock(1234);
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
            [
                new OberonSymbolExport("Clock", 1, 0x20),
                new OberonSymbolExport("SetClock", 1, 0x24),
                new OberonSymbolExport("Time", 1, 0x28),
                new OberonSymbolExport("MarkSector", 1, 0x2c),
                new OberonSymbolExport("AllocSector", 1, 0x30)
            ]);
        var binding = OberonKernelHostBinding.Create(symbol, kernel, 0x3000, 0x4000);
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);

        runtime.BindExternal(binding, machine.HostCalls);
        TestAssertions.Equal(0x10000u, machine.Memory.Read32(0x3008));
        TestAssertions.Equal((uint)machine.Memory.RamSize, machine.Memory.Read32(0x3018));

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
        TestAssertions.Equal(1u, machine.Memory.Read32(0x3004));

        machine.Cpu.SetRegister(0, 0);
        machine.Cpu.SetRegister(1, 0x500);
        machine.Cpu.SetProgramCounter(0x4030);
        machine.Step();
        TestAssertions.Equal(1856u, machine.Memory.Read32(0x500));
        TestAssertions.Equal(2u, machine.Memory.Read32(0x3004));
    }

    public static void RejectsUnsupportedKernelProcedure()
    {
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
                [new OberonSymbolExport("Install", 1, 0x20)]);
        var binding = OberonKernelHostBinding.Create(symbol, new OberonKernel(new Memory()), 0x3000, 0x4000);
        var machine = new Machine();
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        runtime.BindExternal(binding, machine.HostCalls);
        machine.Cpu.SetProgramCounter(0x4020);

        TestAssertions.Throws<OberonHostCallException>(() => machine.Step());
    }

    public static void DispatchesHeapAllocationService()
    {
        var machine = new Machine();
        var kernel = new OberonKernel(machine.Memory);
        var symbol = new OberonSymbolModule(
            "Kernel",
            1,
            1,
            [new OberonSymbolExport("New", 1, 0x20)]);
        var binding = OberonKernelHostBinding.Create(symbol, kernel, 0x3000, 0x4000);
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(symbol);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
        runtime.BindExternal(binding, machine.HostCalls);

        machine.Memory.Write32(0x500, 16);
        machine.Cpu.SetRegister(0, 0x600);
        machine.Cpu.SetRegister(1, 0x500);
        machine.Cpu.SetRegister(15, 0x200);
        machine.Cpu.SetProgramCounter(0x4020);
        machine.Step();

        var pointer = machine.Memory.Read32(0x600);
        TestAssertions.Equal(0x10008u, pointer);
        TestAssertions.Equal(0x500u, machine.Memory.Read32(pointer - 8));
        TestAssertions.Equal(0u, machine.Memory.Read32(pointer - 4));
        TestAssertions.Equal(0u, machine.Memory.Read32(pointer));
        TestAssertions.Equal(16, kernel.AllocatedBytes);
    }

    public static void DispatchesSectorIoServices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-kernel-{Guid.NewGuid():N}.img");
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
            }

            using var disk = new DiskImage();
            disk.Open(path);
            var machine = new Machine();
            var kernel = new OberonKernel(machine.Memory, disk);
            var symbol = new OberonSymbolModule(
                "Kernel",
                1,
                1,
                [
                    new OberonSymbolExport("GetSector", 1, 0x20),
                    new OberonSymbolExport("PutSector", 1, 0x24)
                ]);
            var binding = OberonKernelHostBinding.Create(symbol, kernel, 0x3000, 0x4000);
            var loader = new OberonModuleLoader(_ => null);
            loader.RegisterExternal(symbol);
            var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);
            runtime.BindExternal(binding, machine.HostCalls);

            var source = new byte[OberonKernel.SectorLength];
            for (var index = 0; index < source.Length; index++)
            {
                source[index] = (byte)((index * 13) & 0xff);
                machine.Memory.Write8(0x600u + checked((uint)index), source[index]);
            }

            machine.Cpu.SetRegister(0, 1856);
            machine.Cpu.SetRegister(1, 0x600);
            machine.Cpu.SetRegister(15, 0x200);
            machine.Cpu.SetProgramCounter(0x4024);
            machine.Step();

            machine.Cpu.SetRegister(0, 1856);
            machine.Cpu.SetRegister(1, 0xA00);
            machine.Cpu.SetProgramCounter(0x4020);
            machine.Step();

            for (var index = 0; index < source.Length; index++)
            {
                TestAssertions.Equal(source[index], machine.Memory.Read8(0xA00u + checked((uint)index)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
