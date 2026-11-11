using ProjectOberon.Emulator;

internal static class CliTraceTests
{
    public static void Run()
    {
        FormatsModuleAndStackRegisters();
    }

    private static void FormatsModuleAndStackRegisters()
    {
        var cpu = new Risc5Cpu(new Memory(4096));
        cpu.SetProgramCounter(0x100);
        cpu.SetRegister(12, 0x200);
        cpu.SetRegister(13, 0x300);
        cpu.SetRegister(14, 0x3f0);
        cpu.SetRegister(15, 0x400);

        var output = MachineTraceFormatter.Format(7, cpu, 0);

        TestAssertions.True(output.Contains("7 pc=000100", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r12=00000200", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r13=00000300", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r14=000003f0", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r15=00000400", StringComparison.Ordinal));
    }
}
