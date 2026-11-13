using ProjectOberon.Emulator;

internal static class CompilerDiagnosticTests
{
    public static void Run()
    {
        FormatsFileLocationAndMessage();
        FormatsMultipleDiagnosticsOnSeparateLines();
        AcceptsDynamicOpenArraySelectorLengthSource();
        ReportsNonAddressableAggregateArgument();
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

    private static void AcceptsDynamicOpenArraySelectorLengthSource()
    {
        const string source = "MODULE Arrays; PROCEDURE Consume(items: ARRAY OF INTEGER); BEGIN END Consume; PROCEDURE Forward(items: ARRAY OF ARRAY OF INTEGER); BEGIN Consume(items[0]); END Forward; BEGIN END Arrays.";
        var module = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(module.Succeeded);
    }

    private static void ReportsNonAddressableAggregateArgument()
    {
        const string source = "MODULE Records; TYPE Pair = RECORD value: INTEGER; END; PROCEDURE Consume(item: Pair); BEGIN END Consume; BEGIN Consume(1); END Records.";
        var module = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(!module.Succeeded);
        TestAssertions.True(module.Diagnostics.Any(diagnostic => diagnostic.Message.Contains(
            "argument item of 'Consume' expects RECORD",
            StringComparison.Ordinal)));
        TestAssertions.True(module.Diagnostics.Any(diagnostic => diagnostic.Message.Contains(
            "aggregate argument 'item' of 'Consume' must be addressable",
            StringComparison.Ordinal)));
    }
}
