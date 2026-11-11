using System.Globalization;

namespace ProjectOberon.Emulator;

public static class Risc5Disassembler
{
    private static readonly string[] AluNames = ["MOV", "LSL", "ASR", "ROR", "AND", "ANN", "IOR", "XOR", "ADD", "SUB", "MUL", "DIV", "FAD", "FSB", "FML", "FDV"];
    private static readonly string[] ConditionNames = ["MI", "EQ", "CS", "VS", "LS", "LT", "LE", "T"];

    public static string Format(uint raw)
    {
        var p = (raw & InstructionBits.P) != 0;
        var q = (raw & InstructionBits.Q) != 0;
        var u = (raw & InstructionBits.U) != 0;
        var v = (raw & InstructionBits.V) != 0;
        var a = (raw >> 24) & 0xf;
        var b = (raw >> 20) & 0xf;
        var op = (raw >> 16) & 0xf;
        var c = raw & 0xf;
        var immediate = raw & 0xffff;

        if (!p)
        {
            var name = AluNames[(int)op];
            if (op == (uint)AluOperation.Mov)
            {
                var source = q
                    ? u ? $"0x{immediate << 16:x8}" : v ? SignExtend(immediate, 16).ToString(CultureInfo.InvariantCulture) : $"0x{immediate:x4}"
                    : !u ? $"r{c}" : !v ? "H" : "PSR";
                return $"{name} r{a}, {source}";
            }

            var operand = q
                ? v ? SignExtend(immediate, 16).ToString(CultureInfo.InvariantCulture) : $"0x{immediate:x4}"
                : $"r{c}";
            var carry = (op == (uint)AluOperation.Add || op == (uint)AluOperation.Sub) && u ? ", carry" : string.Empty;
            return $"{name} r{a}, r{b}, {operand}{carry}";
        }

        if (!q)
        {
            var name = u ? "STR" : "LDR";
            var suffix = v ? ".B" : string.Empty;
            var offset = SignExtend(raw & InstructionBits.MemoryOffsetMask, 20);
            return $"{name}{suffix} r{a}, [r{b}, {offset.ToString(CultureInfo.InvariantCulture)}]";
        }

        if (!u && !v && (raw & 0x30u) != 0)
        {
            if ((raw & 0x10u) != 0) return "RTI";
            var state = (raw & 1u) != 0 ? "ON" : "OFF";
            return $"IE {state}";
        }

        var condition = ConditionNames[(int)((raw >> 24) & 7)];
        if ((raw & InstructionBits.Invert) != 0) condition = "N" + condition;
        var target = u
            ? $"{SignExtend(raw & InstructionBits.BranchDisplacementMask, 22).ToString(CultureInfo.InvariantCulture)}"
            : $"r{c}";
        return $"B{condition}{(v ? "L" : string.Empty)} {target}";
    }

    private static int SignExtend(uint value, int bits)
    {
        var shift = 32 - bits;
        return unchecked((int)(value << shift)) >> shift;
    }
}
