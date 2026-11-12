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
}

internal static class OberonCompilerScopeTestExtensions
{
    public static OberonCompilerObject? FindLocalForTest(this OberonCompilerScope scope, string name)
    {
        return scope.Objects.FirstOrDefault(candidate => candidate.Name == name);
    }
}
