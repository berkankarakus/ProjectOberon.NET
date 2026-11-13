using ProjectOberon.Emulator;

internal static class OberonCompilerParserTests
{
    public static void Run()
    {
        ParsesModuleDeclarations();
        ParsesProcedureDeclarations();
        ParsesNestedProcedureCapture();
        AcceptsIndexedVarArguments();
        AcceptsPointerValuesAndNil();
        ParsesPointerDereference();
        ParsesForStatements();
        ParsesCaseStatements();
        ParsesCaseLabelRanges();
        ParsesSetMembership();
        ParsesSupportedLocalTypeTest();
        ParsesRecordExtensionTypeTest();
        ParsesRecursivePointerFieldType();
        ReportsInvalidTypeTestWithoutDuplicateDiagnostic();
        ImportsExternalProcedures();
        RejectsWrongExternalProcedureArity();
        RejectsMismatchedExternalFixedArrayLength();
        ReportsMissingImportedExport();
        ParsesQualifiedImportedType();
        ParsesQualifiedImportedRecordType();
        ParsesNestedQualifiedImportedRecordType();
        ParsesQualifiedImportedArrayType();
        ParsesImportedArrayRecordSelectors();
        ParsesImportedPointerRecordSelectors();
        ParsesExternalRecordFunctionResult();
        RejectsMismatchedImportedRecordParameterSize();
        ParsesSetLiteral();
        ParsesModuleStatements();
        ParsesArrayIndexSelectors();
        ParsesControlFlowStatements();
        FoldsConstantExpressions();
        ReportsDeclarationErrors();
        ReportsUndefinedRuntimeNames();
        ReportsAssignmentTypeErrors();
        ReportsCallTypeErrors();
        ReportsReturnTypeErrors();
        ReportsConditionTypeErrors();
        ReportsModuleNameErrors();
    }

    private static void ParsesModuleDeclarations()
    {
        const string source = """
            MODULE Demo;
            IMPORT IO := Files, System;
            CONST answer* = 2AH; flag = TRUE;
            TYPE
              Point* = RECORD x*, y: INTEGER; END;
              PointRef = POINTER TO Point;
            VAR origin*: PointRef; counts: ARRAY 4 OF INTEGER;
            BEGIN
            END Demo.
            """;

        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.Equal("Demo", result.ModuleName);
        TestAssertions.Equal(0, result.Diagnostics.Count);
        var scope = result.ModuleScope!;
        var answer = Find(scope, "answer");
        TestAssertions.Equal(OberonCompilerObjectClass.Constant, answer!.Class);
        TestAssertions.Equal(42L, answer.Value);
        TestAssertions.True(answer.Exported);

        var point = Find(scope, "Point")!.Type!;
        TestAssertions.Equal(OberonCompilerTypeForm.Record, point.Form);
        TestAssertions.Equal(8, point.Size);
        TestAssertions.True(Find(scope, "x") is null);
        TestAssertions.Equal(2, point.Fields.Count);
        TestAssertions.True(point.Fields[0].Exported);

        var pointer = Find(scope, "PointRef")!.Type!;
        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, pointer.Form);
        TestAssertions.True(ReferenceEquals(point, pointer.Base));
        TestAssertions.Equal(OberonCompilerTypeForm.Array, Find(scope, "counts")!.Type!.Form);
        TestAssertions.Equal(4L, Find(scope, "counts")!.Value);
        TestAssertions.True(Find(scope, "IO")!.ReadOnly);
        TestAssertions.Equal("Files", Find(scope, "IO")!.OriginalName);
    }

    private static void ReportsDeclarationErrors()
    {
        var result = new OberonCompilerParser("MODULE Broken; VAR value: Missing; END Broken.").ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Count > 0);
    }

    private static void ReportsUndefinedRuntimeNames()
    {
        const string source = "MODULE Broken; VAR value: INTEGER; BEGIN value := missing; END Broken.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("missing", StringComparison.Ordinal)));
    }

    private static void ReportsAssignmentTypeErrors()
    {
        const string source = "MODULE Broken; VAR value: INTEGER; flag: BOOLEAN; BEGIN value := flag; END Broken.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("cannot assign", StringComparison.Ordinal)));
    }

    private static void ReportsCallTypeErrors()
    {
        const string source = "MODULE Broken; VAR flag: BOOLEAN; PROCEDURE Takes(value: INTEGER; VAR target: INTEGER); BEGIN END Takes; BEGIN Takes(flag, 1); END Broken.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.Equal(1, result.Diagnostics.Count(diagnostic =>
            diagnostic.Message.Contains("expects INTEGER", StringComparison.Ordinal)));
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("VAR argument", StringComparison.Ordinal)));
    }

    private static void ReportsReturnTypeErrors()
    {
        const string functionSource = "MODULE Broken; PROCEDURE Get(flag: BOOLEAN): INTEGER; BEGIN RETURN flag; END Get; END Broken.";
        var functionResult = new OberonCompilerParser(functionSource).ParseModule();
        TestAssertions.False(functionResult.Succeeded);
        TestAssertions.True(functionResult.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("returns BOOLEAN", StringComparison.Ordinal)));

        const string procedureSource = "MODULE Broken; PROCEDURE Put; BEGIN RETURN 1; END Put; END Broken.";
        var procedureResult = new OberonCompilerParser(procedureSource).ParseModule();
        TestAssertions.False(procedureResult.Succeeded);
        TestAssertions.True(procedureResult.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("cannot return a value", StringComparison.Ordinal)));

        const string partialFunctionSource = "MODULE Broken; PROCEDURE Maybe(flag: BOOLEAN): INTEGER; BEGIN IF flag THEN RETURN 1; END; END Maybe; END Broken.";
        var partialFunctionResult = new OberonCompilerParser(partialFunctionSource).ParseModule();
        TestAssertions.False(partialFunctionResult.Succeeded);
        TestAssertions.True(partialFunctionResult.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("may complete without returning", StringComparison.Ordinal)));

        const string completeFunctionSource = "MODULE Complete; PROCEDURE Get(flag: BOOLEAN): INTEGER; BEGIN IF flag THEN RETURN 1; ELSE RETURN 2; END; END Get; END Complete.";
        var completeFunctionResult = new OberonCompilerParser(completeFunctionSource).ParseModule();
        TestAssertions.True(completeFunctionResult.Succeeded);
        TestAssertions.False(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            FindProcedure(completeFunctionResult.Procedures!, "Get").Body));
    }

    private static void ReportsConditionTypeErrors()
    {
        const string source = "MODULE Broken; VAR value: INTEGER; BEGIN IF value THEN END; END Broken.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("IF condition must be BOOLEAN", StringComparison.Ordinal)));
    }

    private static void ReportsModuleNameErrors()
    {
        var result = new OberonCompilerParser("MODULE Demo; END Other.").ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("module name", StringComparison.OrdinalIgnoreCase)));
    }

    private static void ParsesProcedureDeclarations()
    {
        const string source = """
            MODULE Procedures;
            TYPE Point = RECORD value: INTEGER; END;
            PROCEDURE Add*(VAR left: INTEGER; right: INTEGER): INTEGER;
            VAR local: INTEGER;
            BEGIN
              local := left + right;
              RETURN local;
            END Add;
            PROCEDURE Shapes(VAR point: Point; values: ARRAY OF INTEGER; count: INTEGER);
            BEGIN
            END Shapes;
            PROCEDURE Outer;
              PROCEDURE Inner;
              BEGIN
              END Inner;
            BEGIN
            END Outer;
            END Procedures.
            """;

        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var scope = result.ModuleScope!;
        var add = Find(scope, "Add")!;
        TestAssertions.Equal(OberonCompilerObjectClass.Procedure, add.Class);
        TestAssertions.True(add.Exported);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, add.Type!.Base!.Form);
        TestAssertions.Equal(2, add.Type.ParameterCount);
        TestAssertions.Equal("left", add.Type.Parameters[0].Name);
        TestAssertions.False(add.Type.Parameters[0].ReadOnly);
        TestAssertions.Equal("right", add.Type.Parameters[1].Name);
        TestAssertions.True(add.Type.Parameters[1].ReadOnly);
        var addDefinition = FindProcedure(result.Procedures!, "Add");
        TestAssertions.Equal(2, addDefinition.Body!.Statements.Count);
        TestAssertions.True(addDefinition.Body.Statements[0] is OberonCompilerAssignmentStatement);
        TestAssertions.True(addDefinition.Body.Statements[1] is OberonCompilerReturnStatement);
        var shapes = Find(scope, "Shapes")!.Type!;
        TestAssertions.Equal(3, shapes.ParameterCount);
        TestAssertions.Equal(4L, shapes.Parameters[0].Value);
        TestAssertions.Equal(8L, shapes.Parameters[1].Value);
        TestAssertions.Equal(-1, shapes.Parameters[1].Type!.Length);
        TestAssertions.Equal(16L, shapes.Parameters[2].Value);
        TestAssertions.Equal(OberonCompilerObjectClass.Procedure, Find(scope, "Outer")!.Class);
        var outerDefinition = FindProcedure(result.Procedures!, "Outer");
        TestAssertions.Equal(1, outerDefinition.NestedProcedures.Count);
        TestAssertions.Equal("Inner", outerDefinition.NestedProcedures[0].Symbol.Name);
    }

    private static void FoldsConstantExpressions()
    {
        const string source = "MODULE Expr; CONST a = 2 + 3 * 4; b = a DIV 5; c = ~FALSE; d = 1 < 2; r = 1.5 + 2.5; nr = -1.5; END Expr.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var scope = result.ModuleScope!;
        TestAssertions.Equal(14L, Find(scope, "a")!.Value);
        TestAssertions.Equal(2L, Find(scope, "b")!.Value);
        TestAssertions.Equal(1L, Find(scope, "c")!.Value);
        TestAssertions.Equal(1L, Find(scope, "d")!.Value);
        TestAssertions.Equal(OberonCompilerTypeForm.Real, Find(scope, "r")!.Type!.Form);
        TestAssertions.Equal(BitConverter.SingleToInt32Bits(4f), checked((int)Find(scope, "r")!.Value));
        TestAssertions.Equal(BitConverter.SingleToInt32Bits(-1.5f), checked((int)Find(scope, "nr")!.Value));
    }

    private static void ParsesModuleStatements()
    {
        const string source = """
            MODULE Statements;
            VAR value: INTEGER; text: ARRAY 8 OF CHAR;
            PROCEDURE Ping(first: INTEGER; second: INTEGER);
            BEGIN
            END Ping;
            BEGIN
              value := 1 + 2 * 3;
              text := "hello";
              Ping(value, value + 1);
              value := value + 1;
            END Statements.
            """;

        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var body = result.Body!;
        TestAssertions.Equal(4, body.Statements.Count);
        var assignment = (OberonCompilerAssignmentStatement)body.Statements[0];
        var target = (OberonCompilerNameExpression)assignment.Target;
        TestAssertions.Equal("value", target.Name);
        var addition = (OberonCompilerBinaryExpression)assignment.Value;
        TestAssertions.Equal(OberonCompilerTokenKind.Plus, addition.Operator);
        var multiplication = (OberonCompilerBinaryExpression)addition.Right;
        TestAssertions.Equal(OberonCompilerTokenKind.Multiply, multiplication.Operator);

        var textAssignment = (OberonCompilerAssignmentStatement)body.Statements[1];
        TestAssertions.True(textAssignment.Value is OberonCompilerStringLiteralExpression literal
            && literal.Value == "hello");

        var call = (OberonCompilerCallStatement)body.Statements[2];
        TestAssertions.Equal("Ping", call.Procedure.Name);
        TestAssertions.Equal(2, call.Arguments.Count);
        TestAssertions.True(body.Statements[3] is OberonCompilerAssignmentStatement);
    }

    private static void ParsesNestedProcedureCapture()
    {
        const string source = "MODULE Nested; PROCEDURE Outer; VAR value: INTEGER; PROCEDURE Inner(delta: INTEGER); BEGIN value := value + delta; END Inner; BEGIN Inner(1); END Outer; END Nested.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var outer = FindProcedure(result.Procedures!, "Outer");
        var inner = outer.NestedProcedures.Single();
        TestAssertions.Equal(2, inner.Symbol.Level);
        TestAssertions.Equal(8L, inner.Symbol.Type!.Parameters[0].Value);

        var assignment = (OberonCompilerAssignmentStatement)inner.Body!.Statements[0];
        var captured = (OberonCompilerNameExpression)assignment.Target;
        TestAssertions.Equal(2, captured.Symbol!.Level);
        TestAssertions.Equal(4L, captured.Symbol.Value);
    }

    private static void AcceptsIndexedVarArguments()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 4 OF INTEGER; VAR values: Values; index: INTEGER; PROCEDURE Touch(VAR value: INTEGER); BEGIN END Touch; BEGIN Touch(values[index]); END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.Equal(0, result.Diagnostics.Count);
    }

    private static void AcceptsPointerValuesAndNil()
    {
        const string source = "MODULE Pointers; TYPE Node = RECORD value: INTEGER; END; IntRef = POINTER TO Node; VAR first, second: IntRef; BEGIN first := NIL; second := first; IF second = NIL THEN first := second; END; END Pointers.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.Equal(0, result.Diagnostics.Count);
    }

    private static void ParsesPointerDereference()
    {
        const string source = "MODULE Pointers; TYPE Node = RECORD value: INTEGER; END; IntRef = POINTER TO Node; VAR pointer: IntRef; BEGIN pointer^.value := 1; END Pointers.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        TestAssertions.True(assignment.Target is OberonCompilerFieldExpression
        {
            Receiver: OberonCompilerDereferenceExpression
        });
    }

    private static void ParsesForStatements()
    {
        const string source = "MODULE Loops; VAR index, total: INTEGER; BEGIN FOR index := 0 TO 3 BY 1 DO total := total + index; END; END Loops.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.True(result.Body!.Statements[0] is OberonCompilerForStatement);
    }

    private static void ParsesCaseStatements()
    {
        const string source = "MODULE Cases; VAR value, result: INTEGER; BEGIN CASE value OF 0: result := 1 | 1, 2: result := 2 ELSE result := 3 END; END Cases.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var conditional = (OberonCompilerCaseStatement)result.Body!.Statements[0];
        TestAssertions.Equal(2, conditional.Arms.Count);
        TestAssertions.Equal(2, conditional.Arms[1].Labels.Count);
        TestAssertions.True(conditional.ElseBody is not null);
    }

    private static void ParsesCaseLabelRanges()
    {
        const string source = "MODULE Cases; VAR value, result: INTEGER; BEGIN CASE value OF 1..3: result := 1 ELSE result := 0 END; END Cases.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var conditional = (OberonCompilerCaseStatement)result.Body!.Statements[0];
        TestAssertions.Equal(1L, conditional.Arms[0].Labels[0].Start);
        TestAssertions.Equal(3L, conditional.Arms[0].Labels[0].End);
    }

    private static void ParsesSetMembership()
    {
        const string source = "MODULE Sets; VAR value: INTEGER; flags: SET; result: BOOLEAN; BEGIN result := value IN flags; END Sets.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.Equal(0, result.Diagnostics.Count);
    }

    private static void ParsesSetLiteral()
    {
        const string source = "MODULE Sets; VAR flags: SET; BEGIN flags := {0, 2..4, 31}; END Sets.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var literal = (OberonCompilerIntegerLiteralExpression)assignment.Value;
        TestAssertions.Equal(unchecked((int)0x8000_001d), checked((int)literal.Value));
        TestAssertions.Equal(OberonCompilerTypeForm.Set, literal.Type.Form);
    }

    private static void ReportsInvalidTypeTestWithoutDuplicateDiagnostic()
    {
        const string source = "MODULE Types; VAR value: INTEGER; BEGIN IF value IS value THEN END; END Types.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.Equal(1, result.Diagnostics.Count(diagnostic =>
            diagnostic.Message.Contains("IS requires", StringComparison.Ordinal)));
        TestAssertions.False(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("not supported", StringComparison.Ordinal)));
    }

    private static void ParsesSupportedLocalTypeTest()
    {
        const string source = "MODULE Types; TYPE Node = RECORD value: INTEGER; END; Ref = POINTER TO Node; VAR node: Ref; BEGIN IF node IS Node THEN END; END Types.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.False(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("IS", StringComparison.Ordinal)));
    }

    private static void ParsesRecordExtensionTypeTest()
    {
        const string source = "MODULE Types; TYPE Base = RECORD value: INTEGER; END; Derived = RECORD (Base) extra: INTEGER; END; Ref = POINTER TO Base; VAR node: Ref; BEGIN IF node IS Derived THEN END; END Types.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
    }

    private static void ParsesRecursivePointerFieldType()
    {
        const string source = "MODULE Types; TYPE Node = RECORD next: POINTER TO Node; value: INTEGER; END; VAR node: Node; BEGIN END Types.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var nodeType = result.ModuleScope!.Objects.Single(obj => obj.Name == "Node").Type!;
        TestAssertions.True(ReferenceEquals(nodeType, nodeType.Fields[0].Type!.Base));
    }

    private static void ImportsExternalProcedures()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 7) { IsProcedure = true, ParameterCount = 0 }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        TestAssertions.True(result.ModuleScope!.Objects.Any(obj => obj.Name == "IO"));
    }

    private static void RejectsWrongExternalProcedureArity()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 7) { IsProcedure = true, ParameterCount = 1 }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("expects 1 arguments", StringComparison.Ordinal)));
    }

    private static void RejectsMismatchedExternalFixedArrayLength()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 7)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = new int?[] { 3 }
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Values = ARRAY 2 OF INTEGER; VAR value: Values; BEGIN IO.Set(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("expects ARRAY", StringComparison.Ordinal)));
    }

    private static void ReportsMissingImportedExport()
    {
        var symbols = new OberonSymbolModule("IO", 0x1234, 1, []);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("module member 'Run' is not defined", StringComparison.Ordinal)));
    }

    private static void ParsesQualifiedImportedType()
    {
        var symbols = new OberonSymbolModule(
            "Types",
            0x1234,
            1,
            [new OberonSymbolExport("Count", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Integer
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Value = Types.Count; VAR value: Value; BEGIN value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var value = result.ModuleScope!.Objects.Single(obj => obj.Name == "Value");
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, value.Type!.Form);
    }

    private static void ParsesQualifiedImportedRecordType()
    {
        var symbols = new OberonSymbolModule(
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
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Value = Types.Node; VAR value: Value; BEGIN value.value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var value = result.ModuleScope!.Objects.Single(obj => obj.Name == "Value");
        TestAssertions.Equal(OberonCompilerTypeForm.Record, value.Type!.Form);
        TestAssertions.Equal(8, value.Type.Size);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, value.Type.Fields[0].Type!.Form);
    }

    private static void ParsesQualifiedImportedArrayType()
    {
        var symbols = new OberonSymbolModule(
            "Types",
            0x1234,
            1,
            [new OberonSymbolExport("Values", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Array,
                ArrayLength = 3,
                BaseTypeForm = (int)OberonCompilerTypeForm.Integer,
                TypeSize = 12
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Values = Types.Values; VAR value: Values; BEGIN value[0] := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var value = result.ModuleScope!.Objects.Single(obj => obj.Name == "value");
        TestAssertions.Equal(OberonCompilerTypeForm.Array, value.Type!.Form);
        TestAssertions.Equal(3, value.Type.Length);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, value.Type.Base!.Form);
    }

    private static void ParsesNestedQualifiedImportedRecordType()
    {
        var symbols = new OberonSymbolModule(
            "Types",
            0x1234,
            1,
            [new OberonSymbolExport("Outer", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 8,
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
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Value = Types.Outer; VAR value: Value; BEGIN value.inner.value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var value = result.ModuleScope!.Objects.Single(obj => obj.Name == "Value");
        TestAssertions.Equal(OberonCompilerTypeForm.Record, value.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, value.Type.Fields[0].Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Integer, value.Type.Fields[0].Type!.Fields[0].Type!.Form);
    }

    private static void ParsesImportedPointerRecordSelectors()
    {
        var symbols = new OberonSymbolModule(
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
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE NodeRef = Types.NodeRef; VAR value: NodeRef; BEGIN value^.value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var value = result.ModuleScope!.Objects.Single(obj => obj.Name == "value");
        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, value.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, value.Type.Base!.Form);
    }

    private static void ParsesImportedArrayRecordSelectors()
    {
        var symbols = new OberonSymbolModule(
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
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Values = Types.Nodes; VAR values: Values; BEGIN values[0].value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var values = result.ModuleScope!.Objects.Single(obj => obj.Name == "values");
        TestAssertions.Equal(OberonCompilerTypeForm.Array, values.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, values.Type.Base!.Form);
    }

    private static void ParsesExternalRecordFunctionResult()
    {
        var factory = new OberonSymbolModule(
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
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Factory; TYPE Node = RECORD value: INTEGER; END; VAR target: Node; BEGIN target := Factory.Make(); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Factory"] = factory })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var target = result.ModuleScope!.Objects.Single(obj => obj.Name == "target");
        TestAssertions.Equal(OberonCompilerTypeForm.Record, target.Type!.Form);
    }

    private static void RejectsMismatchedImportedRecordParameterSize()
    {
        var io = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Consume", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [8],
                ParameterRecordFields = [[]]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Node = RECORD value: INTEGER; END; VAR value: Node; BEGIN IO.Consume(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = io })
            .ParseModule();

        TestAssertions.False(result.Succeeded);
        TestAssertions.True(result.Diagnostics.Any(diagnostic =>
            diagnostic.Message.Contains("expects RECORD", StringComparison.Ordinal)));
    }

    private static void ParsesControlFlowStatements()
    {
        const string source = """
            MODULE Flow;
            VAR value: INTEGER;
            BEGIN
              IF value = 0 THEN
                value := 1;
              ELSIF value = 1 THEN
                value := 2;
              ELSE
                value := 3;
              END;
              WHILE value < 4 DO
                value := value + 1;
              END;
              REPEAT
                value := value - 1;
              UNTIL value = 0;
            END Flow.
            """;

        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var body = result.Body!;
        var ifStatement = (OberonCompilerIfStatement)body.Statements[0];
        TestAssertions.Equal(2, ifStatement.Branches.Count);
        TestAssertions.Equal(1, ifStatement.Branches[0].Body.Statements.Count);
        TestAssertions.Equal(1, ifStatement.Branches[1].Body.Statements.Count);
        TestAssertions.Equal(1, ifStatement.ElseBody!.Statements.Count);
        TestAssertions.True(body.Statements[1] is OberonCompilerWhileStatement);
        TestAssertions.True(body.Statements[2] is OberonCompilerRepeatStatement);
    }

    private static void ParsesArrayIndexSelectors()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 4 OF INTEGER; VAR values: Values; index: INTEGER; BEGIN values[index] := values[index] + 1; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        TestAssertions.True(assignment.Target is OberonCompilerIndexExpression);
        TestAssertions.True(assignment.Value is OberonCompilerBinaryExpression
            {
                Left: OberonCompilerIndexExpression
            });
    }

    private static OberonCompilerObject? Find(OberonCompilerScope scope, string name) =>
        scope.Objects.FirstOrDefault(candidate => candidate.Name == name);

    private static OberonCompilerProcedureDefinition FindProcedure(
        IReadOnlyList<OberonCompilerProcedureDefinition> procedures,
        string name) =>
        procedures.Single(procedure => procedure.Symbol.Name == name);
}
