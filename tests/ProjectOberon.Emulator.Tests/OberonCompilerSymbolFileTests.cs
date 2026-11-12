using ProjectOberon.Emulator;

internal static class OberonCompilerSymbolFileTests
{
    public static void Run()
    {
        WritesFilesAcceptedByTheExistingReader();
        ReadsFunctionResultMetadata();
        ReadsPointerFunctionResultMetadata();
        ReadsTypeFormMetadata();
        ReadsArrayTypeMetadata();
        ReadsRecordTypeSizeMetadata();
        ReadsOpenArrayParameterMetadata();
        ReadsPointerParameterMetadata();
        ReadsRecordFieldMetadata();
        ReadsNestedRecordFieldMetadata();
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

    private static void ReadsFunctionResultMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var functionType = symbols.CreateProcedure(symbols.IntegerType, []);
        var function = symbols.Declare(
            "Value",
            OberonCompilerObjectClass.Function,
            functionType,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [function]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.True(module.Exports[0].IsProcedure == true);
        TestAssertions.True(module.Exports[0].HasResult == true);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, module.Exports[0].ResultTypeForm);
    }

    private static void ReadsPointerFunctionResultMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        var pointer = symbols.CreatePointer(record);
        var functionType = symbols.CreateProcedure(pointer, []);
        var function = symbols.Declare(
            "Make",
            OberonCompilerObjectClass.Function,
            functionType,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [function]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Pointer, module.Exports[0].ResultTypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, module.Exports[0].ResultBaseTypeForm);
        TestAssertions.Equal(0, module.Exports[0].ResultBaseTypeSize);
    }

    private static void ReadsTypeFormMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var type = symbols.Declare(
            "Count",
            OberonCompilerObjectClass.Type,
            symbols.IntegerType,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [type]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, module.Exports[0].TypeForm);
    }

    private static void ReadsRecordTypeSizeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        var type = symbols.Declare(
            "Node",
            OberonCompilerObjectClass.Type,
            record,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [type]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, module.Exports[0].TypeForm);
        TestAssertions.Equal(0, module.Exports[0].TypeSize);
    }

    private static void ReadsArrayTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var array = symbols.CreateArray(symbols.IntegerType, 3, 12);
        var type = symbols.Declare(
            "Values",
            OberonCompilerObjectClass.Type,
            array,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [type]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, module.Exports[0].TypeForm);
        TestAssertions.Equal(3, module.Exports[0].ArrayLength);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, module.Exports[0].BaseTypeForm);
        TestAssertions.True(module.Exports[0].BaseTypeSize is null);
        TestAssertions.Equal(12, module.Exports[0].TypeSize);
    }

    private static void ReadsOpenArrayParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var array = symbols.CreateArray(symbols.IntegerType, -1, 8);
        var parameter = symbols.Declare(
            "items",
            OberonCompilerObjectClass.Parameter,
            array,
            readOnly: true);
        var procedureType = symbols.CreateProcedure(symbols.NoneType, [parameter]);
        var procedure = symbols.Declare(
            "Consume",
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [procedure]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, module.Exports[0].ParameterTypeForms![0]);
        TestAssertions.Equal(-1, module.Exports[0].ParameterArrayLengths![0]);
    }

    private static void ReadsPointerParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        var pointer = symbols.CreatePointer(record);
        var parameter = symbols.Declare(
            "value",
            OberonCompilerObjectClass.Parameter,
            pointer,
            readOnly: true);
        var procedureType = symbols.CreateProcedure(symbols.NoneType, [parameter]);
        var procedure = symbols.Declare(
            "Set",
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [procedure]);
        var module = OberonSymbolModuleReader.Parse(data);

        TestAssertions.Equal((int)OberonCompilerTypeForm.Pointer, module.Exports[0].ParameterTypeForms![0]);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, module.Exports[0].ParameterBaseTypeForms![0]);
        TestAssertions.Equal(0, module.Exports[0].ParameterBaseTypeSizes![0]);
    }

    private static void ReadsRecordFieldMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        symbols.DeclareField(record, "enabled", symbols.BooleanType, 4, exported: true);
        var type = symbols.Declare(
            "Node",
            OberonCompilerObjectClass.Type,
            record,
            exported: true);
        var data = OberonCompilerSymbolFileWriter.Write("Demo", [type]);
        var module = OberonSymbolModuleReader.Parse(data);

        var fields = module.Exports[0].RecordFields!;
        TestAssertions.Equal(2, fields.Count);
        TestAssertions.Equal("value", fields[0].Name);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, fields[0].TypeForm);
        TestAssertions.Equal(0, fields[0].Offset);
        TestAssertions.Equal("enabled", fields[1].Name);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Boolean, fields[1].TypeForm);
        TestAssertions.Equal(4, fields[1].Offset);
    }

    private static void ReadsNestedRecordFieldMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var inner = symbols.CreateRecord();
        symbols.DeclareField(inner, "value", symbols.IntegerType, 0, exported: true);
        var outer = symbols.CreateRecord();
        symbols.DeclareField(outer, "inner", inner, 0, exported: true);
        var type = symbols.Declare(
            "Outer",
            OberonCompilerObjectClass.Type,
            outer,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [type]));

        var nested = module.Exports[0].RecordFields![0].RecordFields!;
        TestAssertions.Equal(1, nested.Count);
        TestAssertions.Equal("value", nested[0].Name);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, nested[0].TypeForm);
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
