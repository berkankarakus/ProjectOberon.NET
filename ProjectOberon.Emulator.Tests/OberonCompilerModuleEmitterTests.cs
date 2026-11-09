using ProjectOberon.Emulator;

internal static class OberonCompilerModuleEmitterTests
{
    public static void Run()
    {
        EmitsParameterlessProcedureCall();
        EmitsValueParameterCall();
        EmitsScalarVarParameterCall();
        EmitsFunctionReturnValue();
        EmitsOpenArrayParameterCall();
        EmitsNestedProcedureCall();
        EmitsOpenArrayAndScalarParameterCall();
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

    private static void EmitsScalarVarParameterCall()
    {
        const string source = "MODULE Calls; VAR result: INTEGER; PROCEDURE Ping(VAR value: INTEGER); BEGIN value := value + 1; END Ping; BEGIN Ping(result); END Calls.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("STR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("LDR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("LDR r0, [r0, 0]", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("LDR r1, [r14, 4]", Risc5Disassembler.Format(generator.Code[7]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[8]));
        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[12]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[13]).StartsWith("BTL "));
    }

    private static void EmitsFunctionReturnValue()
    {
        const string source = "MODULE Functions; VAR result: INTEGER; PROCEDURE Double(value: INTEGER): INTEGER; BEGIN RETURN value + value; END Double; BEGIN result := Double(3); END Functions.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[5]));
        TestAssertions.Equal("MOV r0, 0x0003", Risc5Disassembler.Format(generator.Code[9]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[10]).StartsWith("BTL "));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[11]));
    }

    private static void EmitsOpenArrayParameterCall()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 3 OF INTEGER; VAR values: Values; PROCEDURE Consume(items: ARRAY OF INTEGER); BEGIN END Consume; BEGIN Consume(values); END Arrays.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        TestAssertions.Equal("SUB r14, r14, 0x000c", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r14, 4]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r1, [r14, 8]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[7]));
        TestAssertions.Equal("MOV r1, 0x0003", Risc5Disassembler.Format(generator.Code[8]));
        TestAssertions.True(Risc5Disassembler.Format(generator.Code[9]).StartsWith("BTL "));
    }

    private static void EmitsNestedProcedureCall()
    {
        const string source = "MODULE Nested; PROCEDURE Outer; PROCEDURE Inner; BEGIN END Inner; BEGIN Inner; END Outer; BEGIN Outer; END Nested.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(2, instructions.Count(instruction => instruction.StartsWith("SUB r14, r14")));
        TestAssertions.Equal(2, instructions.Count(instruction => instruction.StartsWith("BTL ")));
    }

    private static void EmitsOpenArrayAndScalarParameterCall()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 3 OF INTEGER; VAR values: Values; PROCEDURE Consume(items: ARRAY OF INTEGER; count: INTEGER); BEGIN END Consume; BEGIN Consume(values, 3); END Arrays.";
        var module = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerModuleEmitter(generator).Emit(module);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Contains("MOV r2, 0x0003"));
    }
}
