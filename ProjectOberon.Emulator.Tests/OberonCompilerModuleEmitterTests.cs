using ProjectOberon.Emulator;

internal static class OberonCompilerModuleEmitterTests
{
    public static void Run()
    {
        EmitsParameterlessProcedureCall();
        EmitsValueParameterCall();
        RejectsVarParameterUntilAddressLoweringExists();
    }

    private static void EmitsParameterlessProcedureCall()
    {
        const string source = "MODULE Calls; PROCEDURE Ping; BEGIN END Ping; BEGIN Ping; END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("SUB r14, r14, 0x0004", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r15, [r14, 0]", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r15, [r14, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("ADD r14, r14, 0x0004", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("BT r15", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[5]).StartsWith("BTL "));
    }

    private static void EmitsValueParameterCall()
    {
        const string source = "MODULE Calls; PROCEDURE Inc(value: INTEGER); BEGIN value := value + 1; END Inc; BEGIN Inc(2); END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("SUB r14, r14, 0x0008", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("LDR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("STR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[6]));
        TestAssertions.Equal("MOV r0, 0x0002", Risc5Disassembler.Format(generator.Code[10]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[11]).StartsWith("BTL "));
    }

    private static void RejectsVarParameterUntilAddressLoweringExists()
    {
        const string source = "MODULE Calls; PROCEDURE Ping(VAR value: INTEGER); BEGIN END Ping; BEGIN Ping(1); END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        TestAssertions.Throws<NotSupportedException>(() =>
            new OberonCompilerModuleEmitter(generator).Emit(module));
    }
}
