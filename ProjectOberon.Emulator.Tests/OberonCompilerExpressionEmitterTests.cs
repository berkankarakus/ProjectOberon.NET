using ProjectOberon.Emulator;

internal static class OberonCompilerExpressionEmitterTests
{
    public static void Run()
    {
        EmitsArithmeticAssignment();
        EmitsModuleVariableLoads();
    }

    private static void EmitsArithmeticAssignment()
    {
        const string source = "MODULE Lower; VAR value: INTEGER; BEGIN value := 2 + 3 * 4; END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerExpressionEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("MOV r0, 0x0002", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r1, 0x0003", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("MOV r2, 0x0004", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("MUL r1, r1, r2", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[5]));
    }

    private static void EmitsModuleVariableLoads()
    {
        const string source = "MODULE Lower; VAR value, result: INTEGER; BEGIN result := value + 1; END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerExpressionEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r1, 0x0001", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r13, 4]", Risc5Disassembler.Format(generator.Code[3]));
    }
}
