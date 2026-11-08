namespace ProjectOberon.Emulator;

public sealed partial class Risc5Cpu
{
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
            else
            {
                _memory.Write32(address, _registers[instruction.A]);
            }
        }
        else
        {
            var value = _memory.Read32(address & ~3u);
            if (instruction.V) value = (value >> (int)((address & 3u) * 8)) & 0xffu;
            WriteRegister(instruction.A, value);
            UpdateNz(value);
        }
    }
}
