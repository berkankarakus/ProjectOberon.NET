namespace ProjectOberon.Emulator;

public static class OberonKernelHostBinding
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        OberonKernel kernel,
        uint dataAddress,
        uint codeAddress)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(kernel);

        var handlers = symbol.Exports
            .Select(export => CreateHandler(symbol.Name, export.Name, kernel))
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
        OberonKernel kernel)
    {
        return procedureName switch
        {
            "InitSecMap" => Return(() => kernel.InitSectorMap()),
            "MarkSector" => context =>
            {
                kernel.MarkSector(checked((int)context.Argument(0)));
                context.Return();
            },
            "FreeSector" => context =>
            {
                kernel.FreeSector(checked((int)context.Argument(0)));
                context.Return();
            },
            "AllocSector" => context =>
            {
                var sector = kernel.AllocSector(checked((int)context.Argument(0)));
                context.Memory.Write32(context.Argument(1), checked((uint)sector));
                context.Return();
            },
            "New" => context =>
            {
                var pointer = kernel.New(context.Argument(1));
                context.Memory.Write32(context.Argument(0), pointer);
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

    private static Action<OberonHostCallContext> Return(Action action)
    {
        return context =>
        {
            action();
            context.Return();
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
