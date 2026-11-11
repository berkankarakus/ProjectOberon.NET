using ProjectOberon.Emulator;

internal static class RuntimeDiagnosticsTests
{
    public static void FormatsMachineState()
    {
        using var machine = new Machine();
        machine.Cpu.SetRegister(14, 0x700);
        machine.Cpu.SetRegister(15, 0x800);
        machine.Cpu.SetProgramCounter(0x100);

        var output = OberonRuntimeDiagnostics.Format(
            "test-app",
            machine,
            new InvalidOperationException("boom"));

        TestAssertions.True(output.Contains("test-app: unhandled runtime error", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("InvalidOperationException: boom", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("pc=0x000100", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r14=0x00000700", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("r15=0x00000800", StringComparison.Ordinal));
    }

    public static void FormatsModuleLoadCode()
    {
        var output = OberonRuntimeDiagnostics.Format(
            "test-app",
            machine: null,
            new OberonModuleLoadException(5, "corrupted module Demo"));

        TestAssertions.True(output.Contains("module-load: code=5", StringComparison.Ordinal));
        TestAssertions.True(output.Contains("corrupted module Demo", StringComparison.Ordinal));
    }
}
