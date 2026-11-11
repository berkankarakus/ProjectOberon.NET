namespace ProjectOberon.Emulator;

public static class OberonKernelHostBinding
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonKernel kernel,
        uint dataAddress,
        uint codeAddress,
        uint entryOffsetScale = 1)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(kernel);
        kernel.SyncModuleData(dataAddress);

        return OberonHostBindingFactory.Create(
            symbol,
            dataAddress,
            codeAddress,
            entryOffsetScale,
            export => CreateHandler(symbol.Name, export.Name, kernel, dataAddress));
    }

    private static Action<OberonHostCallContext> CreateHandler(
        string moduleName,
        string procedureName,
        OberonKernel kernel,
        uint dataAddress)
    {
        return procedureName switch
        {
            "Init" => context =>
            {
                kernel.Initialize();
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "InitSecMap" => context =>
            {
                kernel.InitSectorMap();
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "MarkSector" => context =>
            {
                kernel.MarkSector(checked((int)context.Argument(0)));
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "FreeSector" => context =>
            {
                kernel.FreeSector(checked((int)context.Argument(0)));
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "AllocSector" => context =>
            {
                var sector = kernel.AllocSector(checked((int)context.Argument(0)));
                context.Memory.Write32(context.Argument(1), checked((uint)sector));
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "New" => context =>
            {
                var pointer = kernel.New(context.Argument(1));
                context.Memory.Write32(context.Argument(0), pointer);
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "Mark" => context =>
            {
                kernel.Mark(context.Argument(0));
                context.Return();
            },
            "Scan" => context =>
            {
                kernel.Scan();
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "GetSector" => context =>
            {
                var data = kernel.GetSector(checked((int)context.Argument(0)));
                context.WriteBytes(context.Argument(1), data);
                context.Return();
            },
            "PutSector" => context =>
            {
                var data = context.ReadBytes(context.Argument(1), OberonKernel.SectorLength);
                kernel.PutSector(checked((int)context.Argument(0)), data);
                context.Return();
            },
            "SetClock" => context =>
            {
                kernel.SetClock(checked((int)context.Argument(0)));
                kernel.SyncModuleData(dataAddress);
                context.Return();
            },
            "Install" => context =>
            {
                var procedureAddress = context.Argument(0);
                var targetAddress = context.Argument(1);
                var displacement = ((long)procedureAddress - targetAddress) / 4 - 1;
                var instruction = unchecked(0xe700_0000u + (uint)displacement);
                context.Memory.Write32(targetAddress, instruction);
                context.Return();
            },
            "Time" => context =>
            {
                context.Cpu.SetRegister(0, unchecked((uint)kernel.Time));
                context.Return();
            },
            "Clock" => context =>
            {
                context.Cpu.SetRegister(0, unchecked((uint)kernel.Clock));
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
}

public sealed class OberonHostCallException : InvalidOperationException
{
    public OberonHostCallException(string message)
        : base(message)
    {
    }
}
