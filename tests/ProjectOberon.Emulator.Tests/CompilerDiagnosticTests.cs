using ProjectOberon.Emulator;

internal static class CompilerDiagnosticTests
{
    public static void Run()
    {
        FormatsFileLocationAndMessage();
        FormatsMultipleDiagnosticsOnSeparateLines();
    }

    private static void FormatsFileLocationAndMessage()
    {
        var diagnostics = new[]
        {
            new OberonCompilerDiagnostic("MODULE expected", 4, 2, 5)
        };

        TestAssertions.Equal(
            "Demo.Mod:2:5: error: MODULE expected",
            OberonCompilerDiagnostics.Format("Demo.Mod", diagnostics));
    }

    private static void FormatsMultipleDiagnosticsOnSeparateLines()
    {
        var diagnostics = new[]
        {
            new OberonCompilerDiagnostic("first", 0, 1, 1),
            new OberonCompilerDiagnostic("second", 3, 4, 2)
        };

        TestAssertions.Equal(
            $"Demo.Mod:1:1: error: first{Environment.NewLine}Demo.Mod:4:2: error: second",
            OberonCompilerDiagnostics.Format("Demo.Mod", diagnostics));
    }
}
