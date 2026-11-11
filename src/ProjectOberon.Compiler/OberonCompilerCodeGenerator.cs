namespace ProjectOberon.Compiler;

public sealed class OberonCompilerCodeGenerator : IOberonCompilerInstructionWriter
{
    public const int StackPointer = OberonCompilerCallingConvention.StackPointer;
    public const int LinkRegister = OberonCompilerCallingConvention.LinkRegister;
    public const int RegisterStackBase = OberonCompilerCallingConvention.RegisterStackBase;

    private readonly List<uint> _code = [];

    public IReadOnlyList<uint> Code => _code;
    public int ProgramCounter => _code.Count;

    internal void Reset() => _code.Clear();

    public void EmitRegister(AluOperation operation, int destination, int left, int right, bool carry = false)
    {
        ValidateRegister(destination);
        ValidateRegister(left);
        ValidateRegister(right);
        var raw = ((uint)destination << 24)
            | ((uint)left << 20)
            | ((uint)operation << 16)
            | (uint)right;
        if (carry) raw |= InstructionBits.U;
        _code.Add(raw);
    }

    public void EmitImmediate(AluOperation operation, int destination, int left, int immediate)
    {
        ValidateRegister(destination);
        ValidateRegister(left);
        var raw = InstructionBits.Q
            | ((uint)destination << 24)
            | ((uint)left << 20)
            | ((uint)operation << 16)
            | (uint)(immediate & 0xffff);
        if (immediate < 0) raw |= InstructionBits.V;
        _code.Add(raw);
    }

    public void EmitMoveImmediate(int destination, int value)
    {
        if (value is >= -0x8000 and <= 0xffff)
        {
            EmitImmediate(AluOperation.Mov, destination, 0, value);
            return;
        }

        var unsigned = unchecked((uint)value);
        EmitUpperMove(destination, (int)(unsigned >> 16));
        var lower = (int)(unsigned & 0xffff);
        if (lower != 0) EmitImmediate(AluOperation.Ior, destination, destination, lower);
    }

    public void EmitMoveFromHigh(int destination)
    {
        ValidateRegister(destination);
        _code.Add(InstructionBits.U
            | ((uint)destination << 24)
            | ((uint)AluOperation.Mov << 16));
    }

    public void EmitUpperMove(int destination, int upper16)
    {
        ValidateRegister(destination);
        var raw = InstructionBits.Q | InstructionBits.U
            | ((uint)destination << 24)
            | ((uint)AluOperation.Mov << 16)
            | (uint)(upper16 & 0xffff);
        _code.Add(raw);
    }

    public void EmitLoad(int destination, int addressRegister, int offset, bool byteValue = false)
    {
        EmitMemory(destination, addressRegister, offset, store: false, byteValue);
    }

    public void EmitStore(int source, int addressRegister, int offset, bool byteValue = false)
    {
        EmitMemory(source, addressRegister, offset, store: true, byteValue);
    }

    public int EmitBranch(ConditionCode condition = ConditionCode.Always, bool link = false)
        => EmitBranch(condition, link, invert: false);

    public int EmitBranch(ConditionCode condition, bool link, bool invert)
    {
        var raw = InstructionBits.P | InstructionBits.Q | InstructionBits.U
            | ((uint)condition << 24)
            | (link ? InstructionBits.V : 0u);
        if (invert) raw |= InstructionBits.Invert;
        _code.Add(raw);
        return ProgramCounter - 1;
    }

    public int EmitBranchRegister(int register, ConditionCode condition = ConditionCode.Always, bool link = false)
    {
        ValidateRegister(register);
        var raw = InstructionBits.P | InstructionBits.Q
            | ((uint)condition << 24)
            | (link ? InstructionBits.V : 0u)
            | (uint)register;
        _code.Add(raw);
        return ProgramCounter - 1;
    }

    public int EmitBranch(OberonCompilerLabel label, ConditionCode condition = ConditionCode.Always, bool link = false)
        => EmitBranch(label, condition, link, invert: false);

    public int EmitBranch(
        OberonCompilerLabel label,
        ConditionCode condition,
        bool link,
        bool invert)
    {
        ArgumentNullException.ThrowIfNull(label);
        var instruction = EmitBranch(condition, link, invert);
        if (label.Position is int position) PatchBranch(instruction, position);
        else label.Fixups.Add(instruction);
        return instruction;
    }

    public void Bind(OberonCompilerLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.Position is not null) throw new InvalidOperationException("label is already bound");
        label.Position = ProgramCounter;
        foreach (var fixup in label.Fixups) PatchBranch(fixup, ProgramCounter);
        label.Fixups.Clear();
    }

    public void EmitReturn() => EmitBranchRegister(LinkRegister);

    public void EmitCall(OberonCompilerLabel target)
    {
        ArgumentNullException.ThrowIfNull(target);
        EmitBranch(target, ConditionCode.Always, link: true);
    }

    public void EmitProcedurePrologue(int frameSize)
    {
        ValidateFrameSize(frameSize);
        EmitImmediate(AluOperation.Sub, StackPointer, StackPointer, frameSize);
        EmitStore(LinkRegister, StackPointer, 0);
    }

    public void EmitSaveParameter(int register, int offset)
    {
        ValidateRegister(register);
        EmitStore(register, StackPointer, offset);
    }

    public void EmitProcedureReturn(int frameSize)
    {
        ValidateFrameSize(frameSize);
        EmitLoad(LinkRegister, StackPointer, 0);
        EmitImmediate(AluOperation.Add, StackPointer, StackPointer, frameSize);
        EmitBranchRegister(LinkRegister);
    }

    private void EmitMemory(int register, int addressRegister, int offset, bool store, bool byteValue)
    {
        ValidateRegister(register);
        ValidateRegister(addressRegister);
        if (offset is < -0x80_000 or > 0x7f_fff)
            throw new ArgumentOutOfRangeException(nameof(offset), "RISC5 memory offset must fit in 20 bits");

        var raw = InstructionBits.P
            | ((uint)register << 24)
            | ((uint)addressRegister << 20)
            | (uint)(offset & 0xfffff);
        if (store) raw |= InstructionBits.U;
        if (byteValue) raw |= InstructionBits.V;
        _code.Add(raw);
    }

    private void PatchBranch(int instruction, int target)
    {
        if ((uint)instruction >= (uint)_code.Count) throw new ArgumentOutOfRangeException(nameof(instruction));
        var displacement = target - instruction - 1;
        if (displacement is < -0x20_0000 or > 0x1f_ffff)
            throw new InvalidOperationException("branch target is outside the RISC5 displacement range");
        _code[instruction] = (_code[instruction] & ~InstructionBits.BranchDisplacementMask)
            | (uint)(displacement & (int)InstructionBits.BranchDisplacementMask);
    }

    private static void ValidateRegister(int register)
    {
        if (register is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(register));
    }

    private static void ValidateFrameSize(int frameSize)
    {
        if (frameSize is < 4 or > 0x7fff)
            throw new ArgumentOutOfRangeException(nameof(frameSize),
                "procedure frame size must fit a positive RISC5 immediate");
    }
}
