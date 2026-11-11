using ProjectOberon.Emulator;

internal static class OberonCompilerRscTests
{
    public static void Run()
    {
        EmitsMinimalRscModule();
        EmitsImportsAndExportedCommands();
    }

    private static void EmitsMinimalRscModule()
    {
        const string source = "MODULE Demo; VAR value: INTEGER; BEGIN value := 42 END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal("Demo", module.Name);
        TestAssertions.Equal(4, module.VariableBytes);
        TestAssertions.Equal(module.Code.Count, module.CodeWords);
        TestAssertions.Equal(0, module.TypeDescriptorBytes);
        TestAssertions.Equal(0, module.StringBytes);
        TestAssertions.Equal((byte)'O', module.Trailer);

        var parsed = RscModuleReader.Parse(RscModuleWriter.Write(module));
        TestAssertions.Equal(module.Name, parsed.Name);
        TestAssertions.True(module.Code.SequenceEqual(parsed.Code));
        TestAssertions.Equal(module.BodyOffset, parsed.BodyOffset);
    }

    private static void EmitsImportsAndExportedCommands()
    {
        const string source = "MODULE Demo; IMPORT Files; PROCEDURE Start*; END Start; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.Imports.Count);
        TestAssertions.Equal("Files", module.Imports[0].Name);
        TestAssertions.Equal(1, module.Commands.Count);
        TestAssertions.Equal("Start", module.Commands[0].Name);
        TestAssertions.Equal(0, module.Commands[0].CodeOffset);
        TestAssertions.True(module.BodyOffset > module.Commands[0].CodeOffset);
    }
}
