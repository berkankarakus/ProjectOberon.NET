namespace ProjectOberon.Emulator;

internal static class OberonSymbolBinding
{
    public static IReadOnlyList<OberonSymbolExport> Procedures(OberonSymbolModule symbol)
    {
        return symbol.Exports
            .Where(IsProcedure)
            .ToArray();
    }

    public static IReadOnlyList<uint> EntryOffsets(
        OberonSymbolModule symbol,
        uint entryOffsetScale)
    {
        var entries = symbol.Exports
            .Where(IsEntry)
            .ToArray();
        var highestEntry = entries
            .Select(export => export.Value)
            .DefaultIfEmpty(-1)
            .Max();
        if (highestEntry < 0)
        {
            return [];
        }

        var offsets = new uint[checked(highestEntry + 1)];
        foreach (var export in entries)
        {
            if (export.Value < 0)
            {
                throw new InvalidDataException(
                    $"negative SMB entry value: {symbol.Name}.{export.Name}");
            }

            offsets[export.Value] = ScaleOffset(export.Value, entryOffsetScale);
        }

        return offsets;
    }

    public static uint ScaleOffset(int value, uint scale)
    {
        if (value < 0)
        {
            throw new InvalidDataException($"negative SMB entry value: {value}");
        }

        return checked((uint)value * scale);
    }

    private static bool IsEntry(OberonSymbolExport export)
    {
        return IsProcedure(export) || export.Class is 2 or 5;
    }

    private static bool IsProcedure(OberonSymbolExport export)
    {
        return export.IsProcedure ?? export.Class == 1;
    }
}
