namespace ProjectOberon.Emulator;

internal static class MemoryMap
{
    public const uint AddressMask = 0x00ff_ffffu;
    public const uint RomBase = 0x00ff_c000u;
    public const int RomWords = 512;
    public const uint IoBase = 0x00ff_ffc0u;
}
