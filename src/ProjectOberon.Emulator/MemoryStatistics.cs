namespace ProjectOberon.Emulator;

public readonly record struct MemoryStatistics(
    ulong Read32Count,
    ulong Write32Count,
    ulong RomRead32Count,
    ulong MmioRead32Count,
    ulong RomWrite32Count,
    ulong MmioWrite32Count);
