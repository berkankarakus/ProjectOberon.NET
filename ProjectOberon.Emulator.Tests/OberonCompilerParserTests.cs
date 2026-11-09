using ProjectOberon.Emulator;

internal static class OberonCompilerParserTests
{
    public static void Run()
    {
        ParsesModuleDeclarations();
        ReportsDeclarationErrors();
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

    private static OberonCompilerObject? Find(OberonCompilerScope scope, string name) =>
        scope.Objects.FirstOrDefault(candidate => candidate.Name == name);
}
