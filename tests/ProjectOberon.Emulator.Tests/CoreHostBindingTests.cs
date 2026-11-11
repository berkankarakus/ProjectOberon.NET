using ProjectOberon.Emulator;

internal static class CoreHostBindingTests
{
    public static void BindsCoreExternalModules()
    {
        var loader = new OberonModuleLoader(_ => null);
        loader.RegisterExternal(new OberonSymbolModule(
            "Kernel", 1, 1, [new OberonSymbolExport("Clock", 1, 0x20)]));
        loader.RegisterExternal(new OberonSymbolModule(
            "Files", 2, 1, [new OberonSymbolExport("Old", 1, 0x20)]));
        loader.RegisterExternal(new OberonSymbolModule(
            "Modules", 3, 1, [new OberonSymbolExport("Init", 1, 0x20)]));

        using var disk = new DiskImage();
        using var machine = new Machine();
        var kernel = new OberonKernel(machine.Memory, disk);
        var files = new OberonFiles(new OberonFileSystem(disk));
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, loader);

        OberonCoreHostBinding.Bind(loader, runtime, kernel, files, machine.HostCalls);

        TestAssertions.True(loader.TryGetExternalSymbol("Kernel", out _));
        TestAssertions.True(machine.HostCalls.Addresses.Contains(OberonCoreHostBinding.KernelCodeAddress + 0x80));
        TestAssertions.True(machine.HostCalls.Addresses.Contains(OberonCoreHostBinding.FilesCodeAddress + 0x80));
        TestAssertions.True(machine.HostCalls.Addresses.Contains(OberonCoreHostBinding.ModulesCodeAddress + 0x80));
    }
}
