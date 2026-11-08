using System.Numerics;

namespace ProjectOberon.Emulator;

public sealed class Risc5Cpu
{
    public const uint ResetPc = 0x003f_f800u;

    private readonly IMemoryBus _memory;
    private readonly uint[] _registers = new uint[32];
    private uint _pc = ResetPc;
    private uint _high;
    private bool _negative;
    private bool _zero;
    private bool _carry;
    private bool _overflow;
    private bool _irqLevel;
    private bool _interruptEnabled;
    private bool _interruptPending;
    private bool _interruptMode;
    private uint _savedPc;
    private bool _savedNegative;
    private bool _savedZero;
    private bool _savedCarry;
    private bool _savedOverflow;

    public Risc5Cpu(IMemoryBus memory) { _memory = memory ?? throw new ArgumentNullException(nameof(memory)); }

    public uint PcWord => _pc;
    public uint PcByte => (_pc << 2) & Memory.AddressMask;
    public uint High => _high;
    public bool Negative => _negative;
    public bool Zero => _zero;
    public bool Carry => _carry;
    public bool Overflow => _overflow;
    public ulong Steps { get; private set; }
    public uint Register(int index) => _registers[index & 15];

    public void SetRegister(int index, uint value)
    {
        if (index is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "RISC5 has 16 general-purpose registers");
        }

        WriteRegister(index, value);
    }

    public void SetProgramCounter(uint byteAddress)
    {
        if ((byteAddress & 3) != 0)
        {
            throw new ArgumentException("program counter must be word-aligned", nameof(byteAddress));
        }

        if (byteAddress > Memory.AddressMask)
        {
            throw new ArgumentOutOfRangeException(nameof(byteAddress));
        }

        _pc = (byteAddress >> 2) & InstructionBits.BranchDisplacementMask;
    }

    public void Reset()
    {
        Array.Clear(_registers);
        _pc = ResetPc;
        _high = 0;
        _negative = _zero = _carry = _overflow = false;
        _irqLevel = _interruptEnabled = _interruptPending = _interruptMode = false;
        _savedPc = 0;
        _savedNegative = _savedZero = _savedCarry = _savedOverflow = false;
        Steps = 0;
    }

    public void Step() => Step(false);

    public void Step(bool irq)
    {
        var instruction = Decode(_memory.ReadInstruction(PcByte));
        var nextPc = (_pc + 1u) & InstructionBits.BranchDisplacementMask;
        var interruptAck = _interruptPending && _interruptEnabled && !_interruptMode;
        var irqRising = irq && !_irqLevel;
        _irqLevel = irq;

        if (irqRising) _interruptPending = true;
        _pc = nextPc;

        if (!instruction.P) ExecuteRegister(instruction);
        else if (!instruction.Q) ExecuteMemory(instruction);
        else if (instruction.U || instruction.V || (instruction.Raw & 0x30u) == 0) ExecuteBranch(instruction, nextPc);
        else
        {
            if ((instruction.Raw & 0x10u) != 0 && _interruptMode) RestoreInterruptState();
            if ((instruction.Raw & 0x20u) != 0) _interruptEnabled = (instruction.Raw & 1u) != 0;
        }

        if (interruptAck)
        {
            _savedPc = _pc;
            _savedNegative = _negative;
            _savedZero = _zero;
            _savedCarry = _carry;
            _savedOverflow = _overflow;
            _pc = 1;
            _interruptPending = false;
            _interruptMode = true;
        }

        Steps++;
    }

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

    private void ExecuteRegister(Instruction instruction)
    {
        var left = _registers[instruction.B];
        var operand = instruction.Q
            ? (instruction.V ? unchecked((uint)SignExtend(instruction.Imm, 16)) : instruction.Imm)
            : _registers[instruction.C];
        var writes = true;
        var result = ExecuteAluOperation(instruction, left, operand, ref writes);
        if (writes)
        {
            WriteRegister(instruction.A, result);
            UpdateNz(result);
        }
    }

    private void ExecuteMemory(Instruction instruction)
    {
        var address = (_registers[instruction.B] + unchecked((uint)SignExtend(instruction.Raw & InstructionBits.MemoryOffsetMask, 20))) & Memory.AddressMask;
        if (instruction.U)
        {
            if (instruction.V)
            {
                var shift = (int)((address & 3u) * 8);
                var mask = 0xffu << shift;
                var old = _memory.Read32(address & ~3u);
                _memory.Write32(address & ~3u, (old & ~mask) | ((_registers[instruction.A] & 0xffu) << shift));
            }
            else _memory.Write32(address, _registers[instruction.A]);
        }
        else
        {
            var value = _memory.Read32(address & ~3u);
            if (instruction.V) value = (value >> (int)((address & 3u) * 8)) & 0xffu;
            WriteRegister(instruction.A, value);
            UpdateNz(value);
        }
    }

    private void ExecuteBranch(Instruction instruction, uint nextPc)
    {
        if (!Condition(instruction.Raw)) return;
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

    private uint ExecuteAluOperation(Instruction instruction, uint left, uint operand, ref bool writes)
    {
        uint result;
        switch ((AluOperation)instruction.Op)
        {
            case AluOperation.Mov:
                if (instruction.Q) result = instruction.U ? instruction.Imm << 16 : instruction.V ? unchecked((uint)SignExtend(instruction.Imm, 16)) : instruction.Imm;
                else if (!instruction.U) result = _registers[instruction.C];
                else if (!instruction.V) result = _high;
                else result = (_negative ? 1u << 31 : 0) | (_zero ? 1u << 30 : 0) |
                    (_carry ? 1u << 29 : 0) | (_overflow ? 1u << 28 : 0) | 0x53u;
                break;
            case AluOperation.Lsl: result = left << (int)(operand & 31u); break;
            case AluOperation.Asr: result = unchecked((uint)(unchecked((int)left) >> (int)(operand & 31u))); break;
            case AluOperation.Ror:
                var shift = operand & 31u;
                result = shift == 0 ? left : (left >> (int)shift) | (left << (int)(32u - shift));
                break;
            case AluOperation.And: result = left & operand; break;
            case AluOperation.Ann: result = left & ~operand; break;
            case AluOperation.Ior: result = left | operand; break;
            case AluOperation.Xor: result = left ^ operand; break;
            case AluOperation.Add:
                result = left + operand + (instruction.U && _carry ? 1u : 0u);
                UpdateAddFlags(left, operand, result, instruction.U && _carry);
                break;
            case AluOperation.Sub:
                result = left - operand - (instruction.U && _carry ? 1u : 0u);
                UpdateSubFlags(left, operand, result, instruction.U && _carry);
                break;
            case AluOperation.Mul:
                if (!instruction.U)
                {
                    var product = (long)unchecked((int)left) * unchecked((int)operand);
                    var bits = unchecked((ulong)product);
                    result = (uint)bits;
                    _high = (uint)(bits >> 32);
                }
                else
                {
                    var product = (ulong)left * operand;
                    result = (uint)product;
                    _high = (uint)(product >> 32);
                }
                break;
            case AluOperation.Div:
                if (operand == 0) { result = uint.MaxValue; _high = left; }
                else if (instruction.U) { result = left / operand; _high = left % operand; }
                else
                {
                    var dividend = unchecked((int)left);
                    var divisor = unchecked((int)operand);
                    var quotient = dividend / divisor;
                    var remainder = dividend % divisor;
                    if (remainder < 0) { quotient--; remainder += divisor; }
                    result = unchecked((uint)quotient);
                    _high = unchecked((uint)remainder);
                }
                break;
            case AluOperation.Fad: result = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(left) + BitConverter.UInt32BitsToSingle(operand)); break;
            case AluOperation.Fsb: result = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(left) - BitConverter.UInt32BitsToSingle(operand)); break;
            case AluOperation.Fml: result = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(left) * BitConverter.UInt32BitsToSingle(operand)); break;
            case AluOperation.Fdv: result = BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(left) / BitConverter.UInt32BitsToSingle(operand)); break;
            default: writes = false; result = 0; break;
        }
        return result;
    }

    private void UpdateNz(uint value) { _negative = (value & InstructionBits.SignBit) != 0; _zero = value == 0; }

    private void UpdateAddFlags(uint left, uint right, uint result, bool carryIn)
    {
        var wide = (ulong)left + right + (carryIn ? 1u : 0u);
        _carry = (wide >> 32) != 0;
        _overflow = ((~(left ^ right) & (left ^ result)) & InstructionBits.SignBit) != 0;
    }

    private void UpdateSubFlags(uint left, uint right, uint result, bool borrowIn)
    {
        var subtrahend = (ulong)right + (borrowIn ? 1u : 0u);
        _carry = (ulong)left < subtrahend;
        _overflow = (((left ^ right) & (left ^ result)) & InstructionBits.SignBit) != 0;
    }

    private void WriteRegister(int index, uint value) { _registers[index & 15] = value; }

    private void RestoreInterruptState()
    {
        _pc = _savedPc;
        _negative = _savedNegative;
        _zero = _savedZero;
        _carry = _savedCarry;
        _overflow = _savedOverflow;
        _interruptMode = false;
    }
}
