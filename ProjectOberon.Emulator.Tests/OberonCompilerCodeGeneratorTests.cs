using ProjectOberon.Emulator;

internal static class OberonCompilerCodeGeneratorTests
{
    public static void Run()
    {
        EmitsRegisterImmediateAndMemoryInstructions();
        PatchesForwardBranches();
        EmitsFullWidthImmediateValues();
    }

    private static void EmitsRegisterImmediateAndMemoryInstructions()
    {
        var generator = new OberonCompilerCodeGenerator();
        generator.EmitRegister(AluOperation.Add, 1, 2, 3);
        generator.EmitImmediate(AluOperation.Sub, 4, 5, -7);
        generator.EmitLoad(6, OberonCompilerCodeGenerator.StackPointer, -4);
        generator.EmitStore(6, OberonCompilerCodeGenerator.StackPointer, 8, byteValue: true);

        TestAssertions.Equal("ADD r1, r2, r3", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("SUB r4, r5, -7", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r6, [r14, -4]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR.B r6, [r14, 8]", Risc5Disassembler.Format(generator.Code[3]));
    }

    private static void PatchesForwardBranches()
    {
        var generator = new OberonCompilerCodeGenerator();
        var done = new OberonCompilerLabel();
        generator.EmitBranch(done, ConditionCode.Eq);
        generator.EmitMoveImmediate(0, 1);
        generator.Bind(done);

        TestAssertions.Equal("BEQ 1", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r0, 0x0001", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void EmitsFullWidthImmediateValues()
    {
        var generator = new OberonCompilerCodeGenerator();
        generator.EmitMoveImmediate(3, unchecked((int)0x1234_5678u));
        generator.EmitReturn();

        TestAssertions.Equal("MOV r3, 0x12340000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("IOR r3, r3, 0x5678", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("BT r15", Risc5Disassembler.Format(generator.Code[2]));
    }
}
