namespace ProjectOberon.Emulator;

public static class OberonFilesHostBinding
{
    private const int RiderArgument = 0;
    // The compiler places a Rider type descriptor in r1 for VAR Rider.
    private const int FirstRiderPayloadArgument = 2;

    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonFiles files,
        uint dataAddress,
        uint codeAddress,
        uint entryOffsetScale = 1)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(files);

        var state = new BindingState(files);
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
            "Init" or "RestoreList" => context => context.Return(),
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
            "Register" => context =>
            {
                state.Files.Register(state.File(context.Argument(0)));
                context.Return();
            },
            "Close" => context =>
            {
                state.Files.Close(state.File(context.Argument(0)));
                context.Return();
            },
            "Purge" => context =>
            {
                state.Files.Purge(state.File(context.Argument(0)));
                context.Return();
            },
            "Delete" => context =>
            {
                WriteResult(
                    context,
                    context.Argument(2),
                    state.Files.Delete(context.ReadCString(context.Argument(0))));
                context.Return();
            },
            "Rename" => context =>
            {
                var renamed = state.Files.Rename(
                    context.ReadCString(context.Argument(0)),
                    context.ReadCString(context.Argument(2)));
                WriteResult(context, context.Argument(4), renamed);
                context.Return();
            },
            "Set" => context =>
            {
                var file = state.File(context.Argument(FirstRiderPayloadArgument));
                var rider = file.Set(checked((int)context.Argument(FirstRiderPayloadArgument + 1)));
                state.SetRider(context, context.Argument(RiderArgument), rider, file);
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
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.ReadInt(out var value);
                context.Memory.Write32(context.Argument(FirstRiderPayloadArgument), unchecked((uint)value));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "ReadByte" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.ReadByte(out var value);
                context.Memory.Write8(context.Argument(FirstRiderPayloadArgument), value);
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "ReadBytes" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                var bytes = new byte[checked((int)context.Argument(FirstRiderPayloadArgument + 2))];
                rider.ReadBytes(bytes);
                context.WriteBytes(context.Argument(FirstRiderPayloadArgument), bytes);
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "Read" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.Read(out var value);
                context.Memory.Write8(context.Argument(FirstRiderPayloadArgument), checked((byte)value));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "ReadNum" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.ReadNum(out var value);
                context.Memory.Write32(context.Argument(FirstRiderPayloadArgument), unchecked((uint)value));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "ReadSet" or "ReadReal" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.ReadInt(out var value);
                context.Memory.Write32(context.Argument(FirstRiderPayloadArgument), unchecked((uint)value));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "ReadString" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                context.WriteCString(
                    context.Argument(FirstRiderPayloadArgument),
                    rider.ReadString(),
                    checked((int)context.Argument(FirstRiderPayloadArgument + 1)));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "WriteByte" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.WriteByte(checked((byte)context.Argument(FirstRiderPayloadArgument)));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "WriteBytes" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.Write(context.ReadBytes(
                    context.Argument(FirstRiderPayloadArgument),
                    checked((int)context.Argument(FirstRiderPayloadArgument + 2))));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "Write" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.Write(checked((char)context.Argument(FirstRiderPayloadArgument)));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "WriteInt" or "WriteSet" or "WriteReal" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.WriteInt(unchecked((int)context.Argument(FirstRiderPayloadArgument)));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "WriteString" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.WriteString(context.ReadCString(
                    context.Argument(FirstRiderPayloadArgument),
                    checked((int)context.Argument(FirstRiderPayloadArgument + 1))));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            "WriteNum" => context =>
            {
                var rider = state.Rider(context.Argument(RiderArgument));
                rider.WriteNum(unchecked((int)context.Argument(FirstRiderPayloadArgument)));
                state.SyncRider(context, context.Argument(RiderArgument), rider);
                context.Return();
            },
            _ => Unsupported(moduleName, procedureName)
        };
    }

    private static void WriteResult(OberonHostCallContext context, uint address, bool succeeded)
    {
        context.Memory.Write32(address, succeeded ? 0u : 2u);
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
