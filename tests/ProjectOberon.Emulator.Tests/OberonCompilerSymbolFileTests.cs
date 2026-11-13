using ProjectOberon.Emulator;

internal static class OberonCompilerSymbolFileTests
{
    public static void Run()
    {
        WritesFilesAcceptedByTheExistingReader();
        ReadsFunctionResultMetadata();
        ReadsPointerFunctionResultMetadata();
        ReadsRecordFunctionResultMetadata();
        RoundTripsRecordFunctionResultSize();
        ReadsArrayRecordFunctionResultMetadata();
        ReadsPointerRecordTypeMetadata();
        ReadsParserGeneratedPointerRecordMetadata();
        ReadsRecursiveRecordPointerMetadata();
        ReadsTypeFormMetadata();
        ReadsArrayTypeMetadata();
        ReadsArrayRecordElementMetadata();
        ReadsParserGeneratedArrayRecordMetadata();
        ReadsRecordFieldArrayRecordMetadata();
        ReadsRecordTypeSizeMetadata();
        ReadsOpenArrayParameterMetadata();
        ReadsPointerParameterMetadata();
        ReadsRecordParameterFieldMetadata();
        ReadsArrayRecordParameterMetadata();
        ReadsRecordFieldMetadata();
        ReadsRepeatedFieldTypeReferences();
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

    private static void RoundTripsRecordFunctionResultSize()
    {
        var parsed = new OberonCompilerParser(
            "MODULE Factory; TYPE Result = RECORD value*: INTEGER; END; END Factory.")
            .ParseModule();
        var record = parsed.ModuleScope!.Objects.Single(obj => obj.Name == "Result").Type!;
        var symbols = new OberonCompilerSymbols();
        var function = symbols.Declare(
            "Make",
            OberonCompilerObjectClass.Function,
            symbols.CreateProcedure(record, []),
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Factory", [function]));

        var export = module.Exports[0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.ResultTypeForm);
        TestAssertions.Equal(4, export.ResultTypeSize);
        TestAssertions.Equal("value", export.ResultRecordFields![0].Name);
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
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
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

    private static void ReadsRecordFunctionResultMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var functionType = symbols.CreateProcedure(record, []);
        var function = symbols.Declare(
            "Make",
            OberonCompilerObjectClass.Function,
            functionType,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [function]));

        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, module.Exports[0].ResultTypeForm);
        TestAssertions.Equal(0, module.Exports[0].ResultTypeSize);
        TestAssertions.Equal(1, module.Exports[0].ResultRecordFields!.Count);
        TestAssertions.Equal("value", module.Exports[0].ResultRecordFields![0].Name);
    }

    private static void ReadsArrayRecordFunctionResultMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var functionType = symbols.CreateProcedure(symbols.CreateArray(record, 2, 8), []);
        var function = symbols.Declare(
            "MakeMany",
            OberonCompilerObjectClass.Function,
            functionType,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [function]));
        var export = module.Exports[0];

        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, export.ResultTypeForm);
        TestAssertions.Equal(2, export.ResultArrayLength);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.ResultBaseTypeForm);
        TestAssertions.Equal("value", export.ResultBaseRecordFields![0].Name);
    }

    private static void ReadsPointerRecordTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var type = symbols.Declare(
            "NodeRef",
            OberonCompilerObjectClass.Type,
            symbols.CreatePointer(record),
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [type]));

        var export = module.Exports[0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Pointer, export.TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.BaseTypeForm);
        TestAssertions.Equal(1, export.BaseRecordFields!.Count);
        TestAssertions.Equal("value", export.BaseRecordFields![0].Name);
    }

    private static void ReadsParserGeneratedPointerRecordMetadata()
    {
        var parsed = new OberonCompilerParser(
            "MODULE Types; TYPE Node* = RECORD value*: INTEGER; END; Ref* = POINTER TO Node; END Types.")
            .ParseModule();
        var reference = parsed.ModuleScope!.Objects.Single(obj => obj.Name == "Ref");
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Types", [reference]));

        var export = module.Exports[0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Pointer, export.TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.BaseTypeForm);
        TestAssertions.Equal("value", export.BaseRecordFields![0].Name);
    }

    private static void ReadsRecursiveRecordPointerMetadata()
    {
        var parsed = new OberonCompilerParser(
            "MODULE Types; TYPE Node* = RECORD next*: POINTER TO Node; END; END Types.")
            .ParseModule();
        var node = parsed.ModuleScope!.Objects.Single(obj => obj.Name == "Node");
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Types", [node]));

        var field = module.Exports[0].RecordFields![0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Pointer, field.TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, field.BaseTypeForm);
        TestAssertions.Equal(4, field.BaseTypeSize);
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

    private static void ReadsParserGeneratedArrayRecordMetadata()
    {
        var parsed = new OberonCompilerParser(
            "MODULE Types; TYPE Node* = RECORD value*: INTEGER; END; Nodes* = ARRAY 2 OF Node; END Types.")
            .ParseModule();
        var nodes = parsed.ModuleScope!.Objects.Single(obj => obj.Name == "Nodes");
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Types", [nodes]));

        var export = module.Exports[0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, export.TypeForm);
        TestAssertions.Equal(2, export.ArrayLength);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.BaseTypeForm);
        TestAssertions.Equal("value", export.BaseRecordFields![0].Name);
    }

    private static void ReadsArrayRecordElementMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var array = symbols.CreateArray(record, 2, 8);
        var type = symbols.Declare(
            "Nodes",
            OberonCompilerObjectClass.Type,
            array,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [type]));

        var export = module.Exports[0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, export.TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, export.BaseTypeForm);
        TestAssertions.Equal(1, export.BaseRecordFields!.Count);
        TestAssertions.Equal("value", export.BaseRecordFields![0].Name);
    }

    private static void ReadsRecordFieldArrayRecordMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var element = symbols.CreateRecord();
        symbols.DeclareField(element, "value", symbols.IntegerType, 0, exported: true);
        var outer = symbols.CreateRecord();
        symbols.DeclareField(outer, "nodes", symbols.CreateArray(element, 2, 8), 0, exported: true);
        var type = symbols.Declare(
            "Container",
            OberonCompilerObjectClass.Type,
            outer,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [type]));

        var field = module.Exports[0].RecordFields![0];
        TestAssertions.Equal((int)OberonCompilerTypeForm.Array, field.TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Record, field.BaseTypeForm);
        TestAssertions.Equal("value", field.BaseRecordFields![0].Name);
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
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
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
        TestAssertions.Equal("value", module.Exports[0].ParameterBaseRecordFields![0]![0].Name);
    }

    private static void ReadsRecordParameterFieldMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var parameter = symbols.Declare(
            "value",
            OberonCompilerObjectClass.Parameter,
            record,
            readOnly: true);
        var procedureType = symbols.CreateProcedure(symbols.NoneType, [parameter]);
        var procedure = symbols.Declare(
            "Consume",
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [procedure]));

        var fields = module.Exports[0].ParameterRecordFields![0]!;
        TestAssertions.Equal(1, fields.Count);
        TestAssertions.Equal("value", fields[0].Name);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, fields[0].TypeForm);
    }

    private static void ReadsArrayRecordParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "value", symbols.IntegerType, 0, exported: true);
        var parameter = symbols.Declare(
            "values",
            OberonCompilerObjectClass.Parameter,
            symbols.CreateArray(record, -1, 8),
            readOnly: true);
        var procedureType = symbols.CreateProcedure(symbols.NoneType, [parameter]);
        var procedure = symbols.Declare(
            "Consume",
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: true);
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Demo", [procedure]));

        var fields = module.Exports[0].ParameterBaseRecordFields![0]!;
        TestAssertions.Equal(1, fields.Count);
        TestAssertions.Equal("value", fields[0].Name);
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

    private static void ReadsRepeatedFieldTypeReferences()
    {
        var parsed = new OberonCompilerParser(
            "MODULE Types; TYPE Pair* = RECORD first*: INTEGER; second*: INTEGER; END; END Types.")
            .ParseModule();
        var pair = parsed.ModuleScope!.Objects.Single(obj => obj.Name == "Pair");
        var module = OberonSymbolModuleReader.Parse(
            OberonCompilerSymbolFileWriter.Write("Types", [pair]));

        var fields = module.Exports[0].RecordFields!;
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, fields[0].TypeForm);
        TestAssertions.Equal((int)OberonCompilerTypeForm.Integer, fields[1].TypeForm);
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
