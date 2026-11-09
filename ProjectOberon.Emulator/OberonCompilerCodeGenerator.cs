namespace ProjectOberon.Emulator;

public sealed class OberonCompilerLabel
{
    internal int? Position { get; set; }
    internal List<int> Fixups { get; } = [];
}

public sealed class OberonCompilerCodeGenerator
{
    public const int StackPointer = 14;
    public const int LinkRegister = 15;
    public const int RegisterStackBase = 10;

    private readonly List<uint> _code = [];

    public IReadOnlyList<uint> Code => _code;
    public int ProgramCounter => _code.Count;

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
    {
        var raw = InstructionBits.P | InstructionBits.Q | InstructionBits.U
            | ((uint)condition << 24)
            | (link ? InstructionBits.V : 0u);
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
    {
        ArgumentNullException.ThrowIfNull(label);
        var instruction = EmitBranch(condition, link);
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
}
