namespace ProjectOberon.Emulator;

public static class OberonCoreHostBinding
{
    public const uint KernelDataAddress = 0x0008_0000;
    public const uint FilesDataAddress = 0x0008_4000;
    public const uint ModulesDataAddress = 0x0008_8000;
    public const uint KernelCodeAddress = 0x00e0_0000;
    public const uint FilesCodeAddress = 0x00e1_0000;
    public const uint ModulesCodeAddress = 0x00e2_0000;

    public static void Bind(
        OberonModuleLoader modules,
        OberonModuleRuntime runtime,
        OberonKernel kernel,
        OberonFiles files,
        OberonHostCallDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(dispatcher);

        runtime.BindExternal(
            OberonKernelHostBinding.Create(
                RequireSymbol(modules, "Kernel"),
                kernel,
                KernelDataAddress,
                KernelCodeAddress,
                entryOffsetScale: 4),
            dispatcher);
        runtime.BindExternal(
            OberonFilesHostBinding.Create(
                RequireSymbol(modules, "Files"),
                files,
                FilesDataAddress,
                FilesCodeAddress,
                entryOffsetScale: 4),
            dispatcher);
        runtime.BindExternal(
            OberonModulesHostBinding.Create(
                RequireSymbol(modules, "Modules"),
                runtime,
                modules,
                ModulesDataAddress,
                ModulesCodeAddress,
                dispatcher,
                entryOffsetScale: 4),
            dispatcher);
    }

    private static OberonSymbolModule RequireSymbol(OberonModuleLoader modules, string name)
    {
        return modules.TryGetExternalSymbol(name, out var symbol)
            ? symbol
            : throw new OberonModuleLoadException(5,
                $"external Oberon symbol module is not available: {name}");
    }
}
