namespace ProjectOberon.Emulator;

public sealed partial class Risc5Cpu
{
    private uint ExecuteAluOperation(Instruction instruction, uint left, uint operand, ref bool writes)
    {
        uint result;
        switch ((AluOperation)instruction.Op)
        {
            case AluOperation.Mov:
                if (instruction.Q)
                {
                    result = instruction.U
                        ? instruction.Imm << 16
                        : instruction.V
                            ? unchecked((uint)SignExtend(instruction.Imm, 16))
                            : instruction.Imm;
                }
                else if (!instruction.U)
                {
                    result = _registers[instruction.C];
                }
                else if (!instruction.V)
                {
                    result = _high;
                }
                else
                {
                    result = (_negative ? 1u << 31 : 0) | (_zero ? 1u << 30 : 0) |
                        (_carry ? 1u << 29 : 0) | (_overflow ? 1u << 28 : 0) | 0x53u;
                }
                break;
            case AluOperation.Lsl:
                result = left << (int)(operand & 31u);
                break;
            case AluOperation.Asr:
                result = unchecked((uint)(unchecked((int)left) >> (int)(operand & 31u)));
                break;
            case AluOperation.Ror:
                var shift = operand & 31u;
                result = shift == 0 ? left : (left >> (int)shift) | (left << (int)(32u - shift));
                break;
            case AluOperation.And:
                result = left & operand;
                break;
            case AluOperation.Ann:
                result = left & ~operand;
                break;
            case AluOperation.Ior:
                result = left | operand;
                break;
            case AluOperation.Xor:
                result = left ^ operand;
                break;
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
                if (operand == 0)
                {
                    result = uint.MaxValue;
                    _high = left;
                }
                else if (instruction.U)
                {
                    result = left / operand;
                    _high = left % operand;
                }
                else
                {
                    var dividend = unchecked((int)left);
                    var divisor = unchecked((int)operand);
                    var quotient = dividend / divisor;
                    var remainder = dividend % divisor;
                    if (remainder < 0)
                    {
                        quotient--;
                        remainder += divisor;
                    }

                    result = unchecked((uint)quotient);
                    _high = unchecked((uint)remainder);
                }
                break;
            case AluOperation.Fad:
                result = BitConverter.SingleToUInt32Bits(
                    BitConverter.UInt32BitsToSingle(left) + BitConverter.UInt32BitsToSingle(operand));
                break;
            case AluOperation.Fsb:
                result = BitConverter.SingleToUInt32Bits(
                    BitConverter.UInt32BitsToSingle(left) - BitConverter.UInt32BitsToSingle(operand));
                break;
            case AluOperation.Fml:
                result = BitConverter.SingleToUInt32Bits(
                    BitConverter.UInt32BitsToSingle(left) * BitConverter.UInt32BitsToSingle(operand));
                break;
            case AluOperation.Fdv:
                result = BitConverter.SingleToUInt32Bits(
                    BitConverter.UInt32BitsToSingle(left) / BitConverter.UInt32BitsToSingle(operand));
                break;
            default:
                writes = false;
                result = 0;
                break;
        }
        return result;
    }

    private void UpdateNz(uint value)
    {
        _negative = (value & InstructionBits.SignBit) != 0;
        _zero = value == 0;
    }

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

    private void WriteRegister(int index, uint value)
    {
        _registers[index & 15] = value;
    }
}
