namespace ProjectOberon.Emulator;

public static class OberonModulesHostBinding
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonModuleRuntime runtime,
        OberonModuleLoader modules,
        uint dataAddress,
        uint codeAddress,
        OberonHostCallDispatcher dispatcher,
        uint entryOffsetScale = 1)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var state = new BindingState(runtime, modules, dispatcher, dataAddress);
        state.SyncModuleData();
        var procedureExports = OberonSymbolBinding.Procedures(symbol);
        var handlers = procedureExports
            .Select(export => CreateHandler(symbol.Name, export.Name, state))
            .ToArray();
        return new OberonExternalModuleBinding(
            symbol.Name,
            dataAddress,
            codeAddress,
            OberonSymbolBinding.EntryOffsets(symbol, entryOffsetScale))
        {
            EntryHandlers = handlers,
            HostCallOffsets = procedureExports
                .Select(export => OberonSymbolBinding.ScaleOffset(export.Value, entryOffsetScale))
                .ToArray()
        };
    }

    private static Action<OberonHostCallContext> CreateHandler(
        string moduleName,
        string procedureName,
        BindingState state)
    {
        return procedureName switch
        {
            "Init" => context =>
            {
                state.SyncModuleData();
                context.Return();
            },
            "Load" => context =>
            {
                var image = state.Runtime.MemoryLoader.Load(context.ReadCString(context.Argument(0)));
                state.Initialize(image);
                context.Memory.Write32(context.Argument(1), image.DescriptorAddress);
                state.SyncModuleData();
                context.Return();
            },
            "ThisCommand" => context =>
            {
                var image = state.Image(context.Argument(0));
                var command = image?.FindCommandAddress(context.ReadCString(context.Argument(1))) ?? 0;
                context.Cpu.SetRegister(0, command);
                state.SyncModuleData();
                context.Return();
            },
            "Free" => context =>
            {
                var name = context.ReadCString(context.Argument(0));
                if (state.Modules.Free(name))
                {
                    state.Forget(name);
                }

                state.SyncModuleData();
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
        private readonly HashSet<string> _initialized = new(StringComparer.Ordinal);
        private readonly uint _dataAddress;

        public BindingState(
            OberonModuleRuntime runtime,
            OberonModuleLoader modules,
            OberonHostCallDispatcher dispatcher,
            uint dataAddress)
        {
            Runtime = runtime;
            Modules = modules;
            Dispatcher = dispatcher;
            _dataAddress = dataAddress;
        }

        public OberonModuleRuntime Runtime { get; }
        public OberonModuleLoader Modules { get; }
        public OberonHostCallDispatcher Dispatcher { get; }

        public void SyncModuleData()
        {
            var memory = Runtime.Memory;
            memory.Write32(_dataAddress + 8, Runtime.MemoryLoader.RootDescriptorAddress);
            memory.Write32(_dataAddress + 12, Runtime.ModuleTableAddress);
            memory.Write32(_dataAddress + 16, Runtime.MemoryLoader.NextAllocationAddress);
            memory.Write32(_dataAddress + 20, 0);
            WriteName(memory, _dataAddress + 24, string.Empty);
            WriteName(memory, _dataAddress + 56, string.Empty);
        }

        public void Initialize(OberonModuleImage image)
        {
            foreach (var importedModule in image.Module.Imports)
            {
                var importedImage = Image(importedModule.Name)
                    ?? throw new OberonModuleLoadException(5,
                        $"module image is not materialized: {importedModule.Name}");
                Initialize(importedImage);
            }

            if (!_initialized.Add(image.Module.Name))
            {
                return;
            }

            if (image.Module.IsExternal)
            {
                return;
            }

            try
            {
                Runtime.Initialize(image, Dispatcher);
            }
            catch
            {
                _initialized.Remove(image.Module.Name);
                throw;
            }
        }

        public void Forget(string name)
        {
            _initialized.Remove(name);
        }

        public OberonModuleImage? Image(uint descriptorAddress)
        {
            return Runtime.MemoryLoader.LoadedImages
                .FirstOrDefault(image => image.DescriptorAddress == descriptorAddress);
        }

        public OberonModuleImage? Image(string name)
        {
            return Runtime.MemoryLoader.LoadedImages
                .FirstOrDefault(image => image.Module.Name == name);
        }

        private static void WriteName(Memory memory, uint address, string value)
        {
            for (var index = 0; index < 32; index++)
            {
                var character = index < value.Length ? value[index] : '\0';
                memory.Write8(address + checked((uint)index), checked((byte)character));
            }
        }
    }
}
