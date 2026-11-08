namespace ProjectOberon.Emulator;

public static class OberonFilesHostBinding
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonFiles files,
        uint dataAddress,
        uint codeAddress)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(files);

        var state = new BindingState(files);
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
            "Old" => context =>
            {
                var file = state.Files.Old(context.ReadCString(context.Argument(0)));
                context.Cpu.SetRegister(0, file is null ? 0u : state.Handle(file));
                context.Return();
            },
            "New" => context =>
            {
                var file = state.Files.New(context.ReadCString(context.Argument(0)));
                context.Cpu.SetRegister(0, state.Handle(file));
                context.Return();
            },
            "Set" => context =>
            {
                var file = state.File(context.Argument(1));
                var rider = file.Set(checked((int)context.Argument(2)));
                state.SetRider(context, context.Argument(0), rider, file);
                context.Return();
            },
            "Length" => context =>
            {
                context.Cpu.SetRegister(0, checked((uint)state.File(context.Argument(0)).Length));
                context.Return();
            },
            "Date" => context =>
            {
                context.Cpu.SetRegister(0, unchecked((uint)state.File(context.Argument(0)).Date));
                context.Return();
            },
            "Pos" => context =>
            {
                context.Cpu.SetRegister(0, checked((uint)state.Rider(context.Argument(0)).Position));
                context.Return();
            },
            "Base" => context =>
            {
                context.Cpu.SetRegister(0, state.Handle(state.Rider(context.Argument(0)).Base));
                context.Return();
            },
            "ReadInt" => context =>
            {
                var rider = state.Rider(context.Argument(0));
                rider.ReadInt(out var value);
                context.Memory.Write32(context.Argument(1), unchecked((uint)value));
                state.SyncRider(context, context.Argument(0), rider);
                context.Return();
            },
            "ReadString" => context =>
            {
                var rider = state.Rider(context.Argument(0));
                context.WriteCString(context.Argument(1), rider.ReadString());
                state.SyncRider(context, context.Argument(0), rider);
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
        private const uint FirstHandle = 0x0080_0000;
        private readonly Dictionary<uint, OberonFile> _filesByHandle = [];
        private readonly Dictionary<OberonFile, uint> _handlesByFile = [];
        private readonly Dictionary<uint, OberonRider> _riders = [];
        private uint _nextHandle = FirstHandle;

        public BindingState(OberonFiles files)
        {
            Files = files;
        }

        public OberonFiles Files { get; }

        public uint Handle(OberonFile file)
        {
            if (_handlesByFile.TryGetValue(file, out var handle))
            {
                return handle;
            }

            handle = _nextHandle;
            _nextHandle = checked(_nextHandle + 4);
            _handlesByFile.Add(file, handle);
            _filesByHandle.Add(handle, file);
            return handle;
        }

        public OberonFile File(uint handle)
        {
            return _filesByHandle.TryGetValue(handle, out var file)
                ? file
                : throw new OberonHostCallException($"unknown Files.File handle: 0x{handle:x8}");
        }

        public OberonRider Rider(uint address)
        {
            return _riders.TryGetValue(address, out var rider)
                ? rider
                : throw new OberonHostCallException($"unknown Files.Rider address: 0x{address:x8}");
        }

        public void SetRider(OberonHostCallContext context, uint address, OberonRider rider, OberonFile file)
        {
            _riders[address] = rider;
            context.Memory.Write32(address + 8, Handle(file));
            SyncRider(context, address, rider);
        }

        public void SyncRider(OberonHostCallContext context, uint address, OberonRider rider)
        {
            context.Memory.Write32(address, rider.Eof ? 1u : 0u);
            context.Memory.Write32(address + 4, unchecked((uint)rider.Result));
            context.Memory.Write32(address + 12, checked((uint)rider.Position));
        }
    }
}
