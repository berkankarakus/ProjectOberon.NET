using ProjectOberon.Emulator;

internal static class OberonCompilerParserTests
{
    public static void Run()
    {
        ParsesModuleDeclarations();
        ParsesProcedureDeclarations();
        ParsesModuleStatements();
        ParsesControlFlowStatements();
        FoldsConstantExpressions();
        ReportsDeclarationErrors();
        ReportsUndefinedRuntimeNames();
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
        TestAssertions.Equal(2, add.Body!.Statements.Count);
        TestAssertions.True(add.Body.Statements[0] is OberonCompilerAssignmentStatement);
        TestAssertions.True(add.Body.Statements[1] is OberonCompilerReturnStatement);
        var shapes = Find(scope, "Shapes")!.Type!;
        TestAssertions.Equal(3, shapes.ParameterCount);
        TestAssertions.Equal(4L, shapes.Parameters[0].Value);
        TestAssertions.Equal(12L, shapes.Parameters[1].Value);
        TestAssertions.Equal(-1, shapes.Parameters[1].Type!.Length);
        TestAssertions.Equal(20L, shapes.Parameters[2].Value);
        TestAssertions.Equal(OberonCompilerObjectClass.Procedure, Find(scope, "Outer")!.Class);
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
            PROCEDURE Ping;
            BEGIN
            END Ping;
            BEGIN
              value := 1 + 2 * 3;
              text := "hello";
              Ping(value, value + 1);
              RETURN value;
            END Statements.
            """;

        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.True(result.Succeeded);
        var body = result.Body!;
        TestAssertions.Equal(4, body.Statements.Count);
        var assignment = (OberonCompilerAssignmentStatement)body.Statements[0];
        TestAssertions.Equal("value", assignment.Target.Name);
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
        TestAssertions.True(body.Statements[3] is OberonCompilerReturnStatement);
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

    private static OberonCompilerObject? Find(OberonCompilerScope scope, string name) =>
        scope.Objects.FirstOrDefault(candidate => candidate.Name == name);
}
