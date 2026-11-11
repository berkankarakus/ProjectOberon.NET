namespace ProjectOberon.Emulator;

public enum AluOperation : byte
{
    Mov = 0, Lsl = 1, Asr = 2, Ror = 3,
    And = 4, Ann = 5, Ior = 6, Xor = 7,
    Add = 8, Sub = 9, Mul = 10, Div = 11,
    Fad = 12, Fsb = 13, Fml = 14, Fdv = 15
}

public enum ConditionCode : byte
{
    Mi = 0, Eq = 1, Cs = 2, Vs = 3,
    Ls = 4, Lt = 5, Le = 6, Always = 7
}

public static class InstructionBits
{
    public const uint P = 0x8000_0000u;
    public const uint Q = 0x4000_0000u;
    public const uint U = 0x2000_0000u;
    public const uint V = 0x1000_0000u;
    public const uint Invert = 0x0800_0000u;
    public const uint ConditionMask = 0x07u;
    public const uint RegisterMask = 0x0fu;
    public const uint ImmediateMask = 0xffffu;
    public const uint MemoryOffsetMask = 0x000f_ffffu;
    public const uint BranchDisplacementMask = 0x003f_ffffu;
    public const uint SignBit = 0x8000_0000u;
}
