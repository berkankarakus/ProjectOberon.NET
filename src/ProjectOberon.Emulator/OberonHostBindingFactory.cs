namespace ProjectOberon.Emulator;

internal static class OberonHostBindingFactory
{
    public static OberonExternalModuleBinding Create(
        OberonSymbolModule symbol,
        uint dataAddress,
        uint codeAddress,
        uint entryOffsetScale,
        Func<OberonSymbolExport, Action<OberonHostCallContext>> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(handlerFactory);

        var procedureExports = OberonSymbolBinding.Procedures(symbol);
        return new OberonExternalModuleBinding(
            symbol.Name,
            dataAddress,
            codeAddress,
            OberonSymbolBinding.EntryOffsets(symbol, entryOffsetScale))
        {
            EntryHandlers = procedureExports.Select(handlerFactory).ToArray(),
            HostCallOffsets = procedureExports
                .Select(export => OberonSymbolBinding.ScaleOffset(export.Value, entryOffsetScale))
                .ToArray()
        };
    }
}
