namespace ProjectOberon.Emulator;

public static class OberonModulesHostBinding
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonModuleRuntime runtime,
        OberonModuleLoader modules,
        uint dataAddress,
        uint codeAddress)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(modules);

        var state = new BindingState(runtime, modules);
        var handlers = symbol.Exports
            .Select(export => CreateHandler(symbol.Name, export.Name, state))
            .ToArray();
        return new OberonExternalModuleBinding(
            symbol.Name,
            dataAddress,
            codeAddress,
            symbol.Exports.Select(export => checked((uint)export.Value)).ToArray())
        {
            EntryHandlers = handlers
        };
    }

    private static Action<OberonHostCallContext> CreateHandler(
        string moduleName,
        string procedureName,
        BindingState state)
    {
        return procedureName switch
        {
            "Init" => context => context.Return(),
            "Load" => context =>
            {
                var image = state.Runtime.MemoryLoader.Load(context.ReadCString(context.Argument(0)));
                context.Memory.Write32(context.Argument(1), image.DescriptorAddress);
                context.Return();
            },
            "ThisCommand" => context =>
            {
                var image = state.Image(context.Argument(0));
                var command = image?.FindCommandAddress(context.ReadCString(context.Argument(1))) ?? 0;
                context.Cpu.SetRegister(0, command);
                context.Return();
            },
            "Free" => context =>
            {
                state.Modules.Free(context.ReadCString(context.Argument(0)));
                context.Return();
            },
            _ => Unsupported(moduleName, procedureName)
        };
    }

    private static Action<OberonHostCallContext> Unsupported(string moduleName, string procedureName)
    {
        return _ => throw new OberonHostCallException(
            $"host binding does not implement {moduleName}.{procedureName}");
    }

    private sealed class BindingState
    {
        public BindingState(OberonModuleRuntime runtime, OberonModuleLoader modules)
        {
            Runtime = runtime;
            Modules = modules;
        }

        public OberonModuleRuntime Runtime { get; }
        public OberonModuleLoader Modules { get; }

        public OberonModuleImage? Image(uint descriptorAddress)
        {
            return Runtime.MemoryLoader.LoadedImages
                .FirstOrDefault(image => image.DescriptorAddress == descriptorAddress);
        }
    }
}
