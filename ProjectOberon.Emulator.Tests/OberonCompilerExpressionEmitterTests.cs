using ProjectOberon.Emulator;

internal static class OberonCompilerExpressionEmitterTests
{
    public static void Run()
    {
        EmitsArithmeticAssignment();
        EmitsModuleVariableLoads();
        EmitsPackedByteLoadsAndStores();
        EmitsControlFlow();
        EmitsRecordFieldLoadAndStore();
        AllocatesScratchRegistersWithoutUsingDestinationPlusOne();
        EmitsFieldAddressThroughDedicatedEmitter();
        EmitsArrayIndexLoadAndStore();
        EmitsThroughInstructionWriterAbstraction();
    }

    private static void EmitsArithmeticAssignment()
    {
        const string source = "MODULE Lower; VAR value: INTEGER; BEGIN value := 2 + 3 * 4; END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

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

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r1, 0x0001", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r13, 4]", Risc5Disassembler.Format(generator.Code[3]));
    }

    private static void EmitsControlFlow()
    {
        const string source = """
            MODULE Flow;
            VAR value: INTEGER;
            BEGIN
              IF value # 0 THEN
                value := 1;
              ELSE
                value := 2;
              END;
              WHILE value < 3 DO
                value := value + 1;
              END;
              REPEAT
                value := value - 1;
              UNTIL value = 0;
            END Flow.
            """;
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BEQ ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BLT ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNEQ ")));
    }

    private static void EmitsPackedByteLoadsAndStores()
    {
        const string source = "MODULE Bytes; VAR first, second: BOOLEAN; BEGIN second := first; END Bytes.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR.B r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR.B r0, [r13, 1]", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void EmitsRecordFieldLoadAndStore()
    {
        const string source = "MODULE Records; TYPE Point = RECORD x, y: INTEGER; END; VAR point: Point; BEGIN point.x := point.y + 1; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r0, r0, 0x0004", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r0, [r0, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("MOV r1, 0x0001", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[5]));
        TestAssertions.Equal("ADD r1, r1, 0x0000", Risc5Disassembler.Format(generator.Code[6]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[7]));
    }

    private static void AllocatesScratchRegistersWithoutUsingDestinationPlusOne()
    {
        var symbols = new OberonCompilerSymbols();
        var expression = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            new OberonCompilerIntegerLiteralExpression(
                symbols.IntegerType,
                2),
            new OberonCompilerIntegerLiteralExpression(
                symbols.IntegerType,
                3));
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerExpressionEmitter(generator).EmitExpression(expression, destination: 11);

        TestAssertions.Equal("MOV r11, 0x0002", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r0, 0x0003", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("ADD r11, r11, r0", Risc5Disassembler.Format(generator.Code[2]));
    }

    private static void EmitsFieldAddressThroughDedicatedEmitter()
    {
        const string source = "MODULE Records; TYPE Point = RECORD x, y: INTEGER; END; VAR point: Point; BEGIN point.x := 1; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerAddressEmitter(generator).EmitAddress(assignment.Target, 0);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r0, r0, 0x0000", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void EmitsArrayIndexLoadAndStore()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 4 OF INTEGER; VAR values: Values; index: INTEGER; BEGIN values[index] := values[index] + 1; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("LDR r2, [r13, 16]", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("MOV r3, 0x0004", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("MUL r2, r2, r3", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("ADD r0, r0, r2", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("LDR r0, [r0, 0]", Risc5Disassembler.Format(generator.Code[5]));
        TestAssertions.Equal("MOV r1, 0x0001", Risc5Disassembler.Format(generator.Code[6]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[7]));
        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[8]));
        TestAssertions.Equal("LDR r2, [r13, 16]", Risc5Disassembler.Format(generator.Code[9]));
        TestAssertions.Equal("MOV r3, 0x0004", Risc5Disassembler.Format(generator.Code[10]));
        TestAssertions.Equal("MUL r2, r2, r3", Risc5Disassembler.Format(generator.Code[11]));
        TestAssertions.Equal("ADD r1, r1, r2", Risc5Disassembler.Format(generator.Code[12]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[13]));
    }

    private static void EmitsThroughInstructionWriterAbstraction()
    {
        var symbols = new OberonCompilerSymbols();
        var expression = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 2),
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 3));
        var writer = new RecordingInstructionWriter();

        new OberonCompilerExpressionEmitter(writer).EmitExpression(expression, destination: 4);

        TestAssertions.Equal("MOV r4, 0x0002", writer.Calls[0]);
        TestAssertions.Equal("MOV r0, 0x0003", writer.Calls[1]);
        TestAssertions.Equal("Add r4, r4, r0", writer.Calls[2]);
    }

    private sealed class RecordingInstructionWriter : IOberonCompilerInstructionWriter
    {
        public List<string> Calls { get; } = [];

        public void EmitRegister(AluOperation operation, int destination, int left, int right, bool carry = false) =>
            Calls.Add($"{operation} r{destination}, r{left}, r{right}");

        public void EmitMoveImmediate(int destination, int value) =>
            Calls.Add($"MOV r{destination}, 0x{value:x4}");

        public void EmitImmediate(AluOperation operation, int destination, int left, int immediate) =>
            throw new NotSupportedException();

        public void EmitLoad(int destination, int addressRegister, int offset, bool byteValue = false) =>
            throw new NotSupportedException();

        public void EmitStore(int source, int addressRegister, int offset, bool byteValue = false) =>
            throw new NotSupportedException();

        public int EmitBranch(ConditionCode condition = ConditionCode.Always, bool link = false) =>
            throw new NotSupportedException();

        public int EmitBranch(ConditionCode condition, bool link, bool invert) =>
            throw new NotSupportedException();

        public int EmitBranchRegister(int register, ConditionCode condition = ConditionCode.Always, bool link = false) =>
            throw new NotSupportedException();

        public int EmitBranch(
            OberonCompilerLabel label,
            ConditionCode condition = ConditionCode.Always,
            bool link = false) => throw new NotSupportedException();

        public int EmitBranch(
            OberonCompilerLabel label,
            ConditionCode condition,
            bool link,
            bool invert) => throw new NotSupportedException();

        public void Bind(OberonCompilerLabel label) => throw new NotSupportedException();
        public void EmitReturn() => throw new NotSupportedException();
        public void EmitCall(OberonCompilerLabel target) => throw new NotSupportedException();
        public void EmitProcedurePrologue(int frameSize) => throw new NotSupportedException();
        public void EmitSaveParameter(int register, int offset) => throw new NotSupportedException();
        public void EmitProcedureReturn(int frameSize) => throw new NotSupportedException();
    }
}
