using ProjectOberon.Emulator;

internal static class MachineTraceFormatter
{
    public static string Format(ulong index, Risc5Cpu cpu, uint instruction)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        return $"{index} pc={cpu.PcByte:x6} ir={instruction:x8} "
            + $"{Risc5Disassembler.Format(instruction)} "
            + $"r12={cpu.Register(12):x8} r13={cpu.Register(13):x8} "
            + $"r14={cpu.Register(14):x8} r15={cpu.Register(15):x8}";
    }

    public static string Format(MachineTraceEntry entry)
    {
        return $"{entry.Step} pc={entry.ProgramCounter:x6} ir={entry.Instruction:x8} "
            + Risc5Disassembler.Format(entry.Instruction);
    }
}
