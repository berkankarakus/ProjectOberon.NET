using ProjectOberon.Emulator;

internal static class OberonCompilerSymbolFileTests
{
    public static void Run()
    {
        WritesFilesAcceptedByTheExistingReader();
        RejectsUnexportedSymbols();
        RejectsInvalidModuleNames();
        RejectsNonAsciiNestedNames();
        RoundTripsSignedNumberBoundaries();
    }

    private static void WritesFilesAcceptedByTheExistingReader()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "x", symbols.IntegerType, 0, exported: true);
        var point = symbols.Declare("Point", OberonCompilerObjectClass.Type, record, exported: true);
        var pointer = symbols.CreatePointer(record);
        symbols.Declare("Origin", OberonCompilerObjectClass.Variable, pointer, value: 12, exported: true);
        symbols.Declare("Answer", OberonCompilerObjectClass.Constant, symbols.IntegerType,
            value: 42, exported: true);
        var procedureType = symbols.CreateProcedure(symbols.NoneType, []);
        symbols.Declare("Run", OberonCompilerObjectClass.Procedure, procedureType, exported: true);

        var data = OberonCompilerSymbolFileWriter.Write("Demo", symbols.Universe.Objects
            .Where(obj => obj.Exported)
            .ToArray());
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal("Demo", module.Name);
        TestAssertions.True(module.Key != 0);
        TestAssertions.Equal(4, module.Exports.Count);
        TestAssertions.Equal("Point", module.Exports[0].Name);
        TestAssertions.Equal("Origin", module.Exports[1].Name);
        TestAssertions.Equal(12, module.Exports[1].Value);
        TestAssertions.Equal("Answer", module.Exports[2].Name);
        TestAssertions.Equal(42, module.Exports[2].Value);
        TestAssertions.True(module.Exports[3].IsProcedure ?? false);
        TestAssertions.Equal(0, data.Length % 4);
    }

    private static void RejectsUnexportedSymbols()
    {
        var symbols = new OberonCompilerSymbols();
        var local = symbols.Declare("local", OberonCompilerObjectClass.Variable, symbols.IntegerType);

        TestAssertions.Throws<ArgumentException>(() => OberonCompilerSymbolFileWriter.Write("Demo", [local]));
        TestAssertions.Throws<ArgumentNullException>(() => OberonCompilerSymbolFileWriter.Write("Demo", [null!]));
    }

    private static void RejectsInvalidModuleNames()
    {
        TestAssertions.Throws<ArgumentException>(() => OberonCompilerSymbolFileWriter.Write(
            new string('D', 32), []));
        TestAssertions.Throws<ArgumentException>(() => OberonCompilerSymbolFileWriter.Write("Démо", []));
    }

    private static void RejectsNonAsciiNestedNames()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "é", symbols.IntegerType, 0, exported: true);
        var type = symbols.Declare("Record", OberonCompilerObjectClass.Type, record, exported: true);

        TestAssertions.Throws<ArgumentException>(() => OberonCompilerSymbolFileWriter.Write("Demo", [type]));
    }

    private static void RoundTripsSignedNumberBoundaries()
    {
        var symbols = new OberonCompilerSymbols();
        symbols.Declare(
            "Minimum",
            OberonCompilerObjectClass.Constant,
            symbols.IntegerType,
            value: int.MinValue,
            exported: true);
        symbols.Declare(
            "Maximum",
            OberonCompilerObjectClass.Constant,
            symbols.IntegerType,
            value: int.MaxValue,
            exported: true);
        symbols.Declare(
            "Zero",
            OberonCompilerObjectClass.Constant,
            symbols.IntegerType,
            value: 0,
            exported: true);

        var data = OberonCompilerSymbolFileWriter.Write("Demo", symbols.Universe.Objects
            .Where(obj => obj.Exported)
            .ToArray());
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal(int.MinValue, module.Exports.Single(export => export.Name == "Minimum").Value);
        TestAssertions.Equal(int.MaxValue, module.Exports.Single(export => export.Name == "Maximum").Value);
        TestAssertions.Equal(0, module.Exports.Single(export => export.Name == "Zero").Value);
    }
}
