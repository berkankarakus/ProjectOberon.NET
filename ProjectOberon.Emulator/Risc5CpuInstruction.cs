namespace ProjectOberon.Emulator;

public sealed partial class Risc5Cpu
{
    private readonly record struct Instruction(uint Raw, bool P, bool Q, bool U, bool V,
        int A, int B, int Op, int C, uint Imm);

    private static Instruction Decode(uint raw) => new(
        raw,
        (raw & InstructionBits.P) != 0,
        (raw & InstructionBits.Q) != 0,
        (raw & InstructionBits.U) != 0,
        (raw & InstructionBits.V) != 0,
        (int)((raw >> 24) & InstructionBits.RegisterMask),
        (int)((raw >> 20) & InstructionBits.RegisterMask),
        (int)((raw >> 16) & InstructionBits.RegisterMask),
        (int)(raw & InstructionBits.RegisterMask),
        raw & InstructionBits.ImmediateMask);

    private static int SignExtend(uint value, int bits)
    {
        var shift = 32 - bits;
        return unchecked((int)(value << shift)) >> shift;
    }

    private bool Condition(uint instruction)
    {
        var cc = (ConditionCode)((instruction >> 24) & InstructionBits.ConditionMask);
        var invert = (instruction & InstructionBits.Invert) != 0;
        var signed = _negative ^ _overflow;
        var result = cc switch
        {
            ConditionCode.Mi => _negative,
            ConditionCode.Eq => _zero,
            ConditionCode.Cs => _carry,
            ConditionCode.Vs => _overflow,
            ConditionCode.Ls => _carry || _zero,
            ConditionCode.Lt => signed,
            ConditionCode.Le => signed || _zero,
            ConditionCode.Always => true,
            _ => false
        };
        return invert ^ result;
    }

    private void ExecuteBranch(Instruction instruction, uint nextPc)
    {
        if (!Condition(instruction.Raw))
        {
            return;
        }
        var displacement = SignExtend(instruction.Raw & InstructionBits.BranchDisplacementMask, 22);
        _pc = instruction.U
            ? unchecked((uint)((int)nextPc + displacement)) & InstructionBits.BranchDisplacementMask
            : (_registers[instruction.C] >> 2) & InstructionBits.BranchDisplacementMask;
        if (instruction.V)
        {
            var link = nextPc << 2;
            WriteRegister(15, link);
            UpdateNz(link);
        }
    }
}
