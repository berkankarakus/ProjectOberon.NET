using ProjectOberon.Emulator;

internal static class OberonCompilerSymbolsTests
{
    public static void Run()
    {
        InitializesUniverseAndSystemBuiltins();
        ResolvesNestedScopesAndReportsDuplicates();
        BuildsRecordsPointersAndProcedures();
        CreatesReadOnlyModules();
        ImportsExternalVariableMetadata();
        ImportsExternalRecordVariableMetadata();
        ImportsExternalArrayVariableMetadata();
        ImportsPrimitiveTypeMetadata();
        ImportsOpaqueRecordTypeMetadata();
        ImportsNestedRecordTypeMetadata();
        ImportsArrayTypeMetadata();
        ImportsArrayOfRecordTypeMetadata();
        ImportsRecordFieldArrayRecordMetadata();
        ImportsPointerRecordTypeMetadata();
        ImportsExternalFunctionResultType();
        ImportsExternalPointerFunctionResultType();
        ImportsExternalRecordFunctionResultType();
        ImportsExternalArrayRecordFunctionResultType();
        ImportsExternalRecordParameterMetadata();
        ImportsExternalArrayRecordParameterMetadata();
        ImportsExternalPointerRecordParameterMetadata();
    }

    private static void InitializesUniverseAndSystemBuiltins()
    {
        var symbols = new OberonCompilerSymbols();

        var integer = symbols.Lookup("INTEGER");
        TestAssertions.True(integer is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Type, integer!.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, integer.Type!.Form);
        TestAssertions.True(ReferenceEquals(symbols.IntegerType, symbols.Lookup("LONGINT")!.Type));
        TestAssertions.Equal(1, symbols.ByteType.Size);
        TestAssertions.Equal(OberonCompilerObjectClass.Function, symbols.SystemModule.FindMember("H")!.Class);
        TestAssertions.Equal(201L, symbols.SystemModule.FindMember("H")!.Value);
    }

    private static void ResolvesNestedScopesAndReportsDuplicates()
    {
        var symbols = new OberonCompilerSymbols();
        var outer = symbols.Declare("value", OberonCompilerObjectClass.Variable, symbols.IntegerType);
        symbols.OpenScope();
        var inner = symbols.Declare("value", OberonCompilerObjectClass.Variable, symbols.BooleanType);

        TestAssertions.True(ReferenceEquals(inner, symbols.Lookup("value")));
        TestAssertions.True(ReferenceEquals(outer, symbols.CurrentScope.Parent!.FindLocalForTest("value")));
        TestAssertions.True(!ReferenceEquals(inner, outer));

        var duplicate = symbols.Declare("value", OberonCompilerObjectClass.Constant, symbols.IntegerType);
        TestAssertions.True(ReferenceEquals(inner, duplicate));
        TestAssertions.Equal(1, symbols.Diagnostics.Count);
        symbols.CloseScope();
        TestAssertions.True(ReferenceEquals(outer, symbols.Lookup("value")));
    }

    private static void BuildsRecordsPointersAndProcedures()
    {
        var symbols = new OberonCompilerSymbols();
        var record = symbols.CreateRecord();
        symbols.DeclareField(record, "x", symbols.IntegerType, 0, exported: true);
        symbols.DeclareField(record, "flag", symbols.BooleanType, 4);
        var pointer = symbols.CreatePointer(record);
        var parameter = symbols.Declare("input", OberonCompilerObjectClass.Parameter,
            symbols.IntegerType, readOnly: true);
        var procedure = symbols.CreateProcedure(symbols.NoneType, [parameter]);

        TestAssertions.Equal(2, record.Fields.Count);
        TestAssertions.True(ReferenceEquals(record.Fields[0], symbols.LookupField(record, "x")));
        TestAssertions.True(ReferenceEquals(record, pointer.Base));
        TestAssertions.Equal(1, procedure.ParameterCount);
        TestAssertions.True(ReferenceEquals(symbols.IntegerType, procedure.Parameters[0].Type));
    }

    private static void CreatesReadOnlyModules()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.OpenModule("Files", "Files", 0x1234);

        TestAssertions.Equal(OberonCompilerObjectClass.Module, module.Class);
        TestAssertions.Equal(0x1234L, module.Key);
        TestAssertions.True(module.ReadOnly);
        TestAssertions.True(ReferenceEquals(module, symbols.Lookup("Files")));
    }

    private static void ImportsExternalVariableMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Config",
            new OberonSymbolModule(
                "Config",
                0x1234,
                1,
                [new OberonSymbolExport("Value", 2, 12)]));
        var variable = module.FindMember("Value");

        TestAssertions.True(variable is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Variable, variable!.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, variable.Type!.Form);
        TestAssertions.Equal(12L, variable.Value);
        TestAssertions.Equal(0, variable.Level);
    }

    private static void ImportsExternalRecordVariableMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "State",
            new OberonSymbolModule(
                "State",
                0x1234,
                1,
                [new OberonSymbolExport("Current", 2, 0)
                {
                    IsProcedure = false,
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 4,
                    RecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var variable = module.FindMember("Current")!;

        TestAssertions.Equal(OberonCompilerTypeForm.Record, variable.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, variable.Type.Fields[0].Type!.Form);
    }

    private static void ImportsExternalArrayVariableMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "State",
            new OberonSymbolModule(
                "State",
                0x1234,
                1,
                [new OberonSymbolExport("Values", 2, 0)
                {
                    IsProcedure = false,
                    TypeForm = (int)OberonCompilerTypeForm.Array,
                    ArrayLength = 2,
                    BaseTypeForm = (int)OberonCompilerTypeForm.Integer,
                    BaseTypeSize = 4,
                    TypeSize = 8
                }]));
        var variable = module.FindMember("Values")!;

        TestAssertions.Equal(OberonCompilerTypeForm.Array, variable.Type!.Form);
        TestAssertions.Equal(2, variable.Type.Length);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, variable.Type.Base!.Form);
    }

    private static void ImportsPrimitiveTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Count", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Integer
                }]));
        var type = module.FindMember("Count");

        TestAssertions.True(type is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Type, type!.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Type!.Form);
    }

    private static void ImportsArrayOfRecordTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Nodes", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Array,
                    ArrayLength = 2,
                    BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                    BaseTypeSize = 4,
                    BaseRecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ],
                    TypeSize = 8
                }]));
        var type = module.FindMember("Nodes")!.Type!;

        TestAssertions.Equal(OberonCompilerTypeForm.Array, type.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, type.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Base.Fields[0].Type!.Form);
    }

    private static void ImportsRecordFieldArrayRecordMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Container", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 8,
                    RecordFields =
                    [
                        new OberonSymbolField(
                            "nodes",
                            0,
                            (int)OberonCompilerTypeForm.Array,
                            8,
                            2,
                            (int)OberonCompilerTypeForm.Record,
                            4,
                            null,
                            [new OberonSymbolField(
                                "value",
                                0,
                                (int)OberonCompilerTypeForm.Integer,
                                4,
                                null,
                                null,
                                null)])
                    ]
                }]));
        var type = module.FindMember("Container")!.Type!;
        var fieldType = type.Fields[0].Type!;

        TestAssertions.Equal(OberonCompilerTypeForm.Array, fieldType.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, fieldType.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, fieldType.Base.Fields[0].Type!.Form);
    }

    private static void ImportsPointerRecordTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("NodeRef", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Pointer,
                    BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                    BaseTypeSize = 4,
                    BaseRecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var type = module.FindMember("NodeRef")!.Type!;

        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, type.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, type.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Base.Fields[0].Type!.Form);
    }

    private static void ImportsExternalFunctionResultType()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Math",
            new OberonSymbolModule(
                "Math",
                0x1234,
                1,
                [new OberonSymbolExport("Measure", 1, 4)
                {
                    IsProcedure = true,
                    HasResult = true,
                    ResultTypeForm = (int)OberonCompilerTypeForm.Real
                }]));
        var function = module.FindMember("Measure");

        TestAssertions.True(function is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Function, function!.Class);
        TestAssertions.True(ReferenceEquals(symbols.RealType, function.Type!.Base));
    }

    private static void ImportsOpaqueRecordTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Node", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 8,
                    RecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var type = module.FindMember("Node");

        TestAssertions.True(type is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Type, type!.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, type.Type!.Form);
        TestAssertions.Equal(8, type.Type.Size);
        TestAssertions.Equal(0, type.Type.Fields[0].Value);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Type.Fields[0].Type!.Form);
    }

    private static void ImportsExternalPointerFunctionResultType()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Math",
            new OberonSymbolModule(
                "Math",
                0x1234,
                1,
                [new OberonSymbolExport("Make", 1, 4)
                {
                    IsProcedure = true,
                    HasResult = true,
                    ResultTypeForm = (int)OberonCompilerTypeForm.Pointer,
                    ResultBaseTypeForm = (int)OberonCompilerTypeForm.Record,
                    ResultBaseTypeSize = 4,
                    ResultBaseRecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var function = module.FindMember("Make");

        TestAssertions.True(function is not null);
        TestAssertions.Equal(OberonCompilerObjectClass.Function, function!.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, function.Type!.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, function.Type.Base.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, function.Type.Base.Base.Fields[0].Type!.Form);
    }

    private static void ImportsArrayTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Values", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Array,
                    ArrayLength = 3,
                    BaseTypeForm = (int)OberonCompilerTypeForm.Integer,
                    BaseTypeSize = 4,
                    TypeSize = 12
                }]));
        var type = module.FindMember("Values");

        TestAssertions.True(type is not null);
        TestAssertions.Equal(OberonCompilerTypeForm.Array, type!.Type!.Form);
        TestAssertions.Equal(3, type.Type.Length);
        TestAssertions.Equal(12, type.Type.Size);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Type.Base!.Form);
    }

    private static void ImportsNestedRecordTypeMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Types",
            new OberonSymbolModule(
                "Types",
                0x1234,
                1,
                [new OberonSymbolExport("Outer", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 4,
                    RecordFields =
                    [
                        new OberonSymbolField(
                            "inner",
                            0,
                            (int)OberonCompilerTypeForm.Record,
                            4,
                            null,
                            null,
                            null,
                            [new OberonSymbolField(
                                "value",
                                0,
                                (int)OberonCompilerTypeForm.Integer,
                                4,
                                null,
                                null,
                                null)])
                    ]
                }]));
        var type = module.FindMember("Outer")!.Type!;

        TestAssertions.Equal(OberonCompilerTypeForm.Record, type.Fields[0].Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, type.Fields[0].Type!.Fields[0].Type!.Form);
    }

    private static void ImportsExternalArrayRecordFunctionResultType()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Factory",
            new OberonSymbolModule(
                "Factory",
                0x1234,
                1,
                [new OberonSymbolExport("MakeMany", 1, 0)
                {
                    IsProcedure = true,
                    HasResult = true,
                    ResultTypeForm = (int)OberonCompilerTypeForm.Array,
                    ResultArrayLength = 2,
                    ResultBaseTypeForm = (int)OberonCompilerTypeForm.Record,
                    ResultBaseTypeSize = 4,
                    ResultBaseRecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var function = module.FindMember("MakeMany")!;

        TestAssertions.Equal(OberonCompilerTypeForm.Array, function.Type!.Base!.Form);
        TestAssertions.Equal(2, function.Type.Base.Length);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, function.Type.Base.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, function.Type.Base.Base.Fields[0].Type!.Form);
    }

    private static void ImportsExternalRecordFunctionResultType()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "Factory",
            new OberonSymbolModule(
                "Factory",
                0x1234,
                1,
                [new OberonSymbolExport("Make", 1, 0)
                {
                    IsProcedure = true,
                    HasResult = true,
                    ResultTypeForm = (int)OberonCompilerTypeForm.Record,
                    ResultBaseTypeSize = 4,
                    ResultRecordFields =
                    [
                        new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }]));
        var function = module.FindMember("Make")!;

        TestAssertions.Equal(OberonCompilerObjectClass.Function, function.Class);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, function.Type!.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, function.Type.Base.Fields[0].Type!.Form);
    }

    private static void ImportsExternalPointerRecordParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "IO",
            new OberonSymbolModule(
                "IO",
                0x1234,
                1,
                [new OberonSymbolExport("Set", 1, 7)
                {
                    IsProcedure = true,
                    ParameterCount = 1,
                    ParameterTypeForms = [(int)OberonCompilerTypeForm.Pointer],
                    ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Record],
                    ParameterBaseTypeSizes = [4],
                    ParameterBaseRecordFields =
                    [
                        [new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)]
                    ]
                }]));
        var parameter = module.FindMember("Set")!.Type!.Parameters[0];

        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, parameter.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, parameter.Type.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, parameter.Type.Base.Fields[0].Type!.Form);
    }

    private static void ImportsExternalArrayRecordParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "IO",
            new OberonSymbolModule(
                "IO",
                0x1234,
                1,
                [new OberonSymbolExport("Consume", 1, 7)
                {
                    IsProcedure = true,
                    ParameterCount = 1,
                    ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                    ParameterArrayLengths = [-1],
                    ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Record],
                    ParameterBaseTypeSizes = [4],
                    ParameterBaseRecordFields =
                    [
                        [new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)]
                    ]
                }]));
        var parameter = module.FindMember("Consume")!.Type!.Parameters[0];

        TestAssertions.Equal(OberonCompilerTypeForm.Array, parameter.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, parameter.Type.Base!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, parameter.Type.Base.Fields[0].Type!.Form);
    }

    private static void ImportsExternalRecordParameterMetadata()
    {
        var symbols = new OberonCompilerSymbols();
        var module = symbols.ImportModule(
            "IO",
            new OberonSymbolModule(
                "IO",
                0x1234,
                1,
                [new OberonSymbolExport("Consume", 1, 7)
                {
                    IsProcedure = true,
                    ParameterCount = 1,
                    ParameterTypeForms = [(int)OberonCompilerTypeForm.Record],
                    ParameterBaseTypeSizes = [4],
                    ParameterRecordFields =
                    [
                        [new OberonSymbolField(
                            "value",
                            0,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)]
                    ]
                }]));
        var procedure = module.FindMember("Consume")!;
        var parameter = procedure.Type!.Parameters[0];

        TestAssertions.Equal(OberonCompilerTypeForm.Record, parameter.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, parameter.Type.Fields[0].Type!.Form);
    }
}

internal static class OberonCompilerScopeTestExtensions
{
    public static OberonCompilerObject? FindLocalForTest(this OberonCompilerScope scope, string name)
    {
        return scope.Objects.FirstOrDefault(candidate => candidate.Name == name);
    }
}
