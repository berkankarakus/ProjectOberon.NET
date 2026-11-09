using ProjectOberon.Emulator;

internal static class OberonCompilerModuleEmitterTests
{
    public static void Run()
    {
        EmitsParameterlessProcedureCall();
        RejectsProcedureArgumentsUntilStackLoweringExists();
    }

    private static void EmitsParameterlessProcedureCall()
    {
        const string source = "MODULE Calls; PROCEDURE Ping; BEGIN RETURN; END Ping; BEGIN Ping; END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("BT r15", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("BT r15", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[2]).StartsWith("BTL "));
    }

    private static void RejectsProcedureArgumentsUntilStackLoweringExists()
    {
        const string source = "MODULE Calls; PROCEDURE Ping(value: INTEGER); BEGIN RETURN; END Ping; BEGIN Ping(1); END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        TestAssertions.Throws<NotSupportedException>(() =>
            new OberonCompilerModuleEmitter(generator).Emit(module));
    }
}
