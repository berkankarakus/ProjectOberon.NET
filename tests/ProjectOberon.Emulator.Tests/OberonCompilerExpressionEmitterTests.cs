using ProjectOberon.Emulator;

internal static class OberonCompilerExpressionEmitterTests
{
    public static void Run()
    {
        EmitsArithmeticAssignment();
        EmitsModuleVariableLoads();
        EmitsPackedByteLoadsAndStores();
        EmitsIntegerRemainder();
        EmitsRealArithmetic();
        FoldsRealConstantExpressions();
        FoldsUnaryRealLiteral();
        EmitsRealComparisons();
        EmitsIntegerComparisonsWithImmediateOperands();
        EmitsReversedIntegerComparisonWithImmediateOperand();
        EmitsBooleanComparisonsWithoutBranches();
        EmitsBooleanLiteralComparisonsWithoutTemporaries();
        EmitsBooleanIdentitiesWithoutLogicalInstructions();
        FoldsBooleanUnaryLiterals();
        FoldsBooleanConstantExpressions();
        EmitsPointerValues();
        EmitsPointerDereference();
        EmitsForLoop();
        EmitsDescendingForLoop();
        EmitsCaseStatement();
        EmitsCaseRange();
        EmitsConstantControlFlowWithoutBranches();
        BranchesDirectlyOnIntegerComparisons();
        BranchesDirectlyOnReversedIntegerComparisons();
        BranchesWithTwoIntegerOperands();
        BranchesWithConstantExpressionImmediate();
        BranchesDirectlyOnIntegerConjunctions();
        BranchesDirectlyOnIntegerDisjunctions();
        BranchesNestedSideEffectFreeConditions();
        FoldsBooleanIdentitiesInBranches();
        BranchesDirectlyOnRealComparisons();
        BranchesDirectlyOnSetMembership();
        BranchesDirectlyOnSetLiteralMembership();
        BranchesDirectlyOnRealAndSetPredicates();
        EmitsSetMembership();
        EmitsSetLiteralMembershipWithImmediate();
        EmitsSetLiteralAndOperations();
        EmitsControlFlow();
        BranchesOnBooleanValuesWithImmediateZero();
        BranchesOnNegatedBooleanValuesWithoutMaterializingNot();
        BranchesDirectlyOnNegatedIntegerComparisons();
        EmitsRecordFieldLoadAndStore();
        EmitsAggregateRecordCopy();
        EmitsConstantAtLastExpressionRegister();
        EmitsFieldAddressThroughDedicatedEmitter();
        EmitsArrayIndexLoadAndStore();
        EmitsConstantExpressionArrayIndexAddress();
        EmitsNonPowerOfTwoArrayIndexWithMultiply();
        EmitsAggregateFieldAddress();
        EmitsAggregateArrayElementAddress();
        EmitsAggregatePointerDereferenceAddress();
        EmitsThroughInstructionWriterAbstraction();
        EmitsLargeIntegerLiteralsThroughInstructionWriter();
        EmitsImmediateOperandsForConstantExpressions();
        EmitsImmediateForConstantLeftArithmetic();
        EmitsComparisonImmediateForConstantExpressions();
        ValidatesImmediateBoundaryOptimizations();
        FoldsIntegerConstantExpressions();
        FoldsUnaryIntegerConstantExpressions();
        EliminatesIntegerIdentityOperations();
        EliminatesIntegerBitwiseIdentityOperations();
    }

    private static void EmitsArithmeticAssignment()
    {
        const string source = "MODULE Lower; VAR value: INTEGER; BEGIN value := 2 + 3 * 4; END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("MOV r0, 0x000e", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void EmitsModuleVariableLoads()
    {
        const string source = "MODULE Lower; VAR value, result: INTEGER; BEGIN result := value + 1; END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r0, r0, 0x0001", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("STR r0, [r13, 4]", Risc5Disassembler.Format(generator.Code[2]));
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
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNLT ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BEQ ")));
    }

    private static void BranchesOnBooleanValuesWithImmediateZero()
    {
        const string source = "MODULE Branch; VAR flag: BOOLEAN; result: INTEGER; BEGIN IF flag THEN result := 1; END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Contains("SUB r0, r0, 0x0000"));
        TestAssertions.False(instructions.Contains("MOV r1, 0x0000"));
    }

    private static void BranchesOnNegatedBooleanValuesWithoutMaterializingNot()
    {
        const string source = "MODULE Branch; VAR flag: BOOLEAN; result: INTEGER; BEGIN IF ~flag THEN result := 1; END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNEQ ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("XOR ")));
    }

    private static void BranchesDirectlyOnNegatedIntegerComparisons()
    {
        const string source = "MODULE Branch; VAR value, result: INTEGER; BEGIN IF ~(value = 1) THEN result := 1; END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(6, instructions.Length);
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BEQ ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("XOR ")));
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

    private static void EmitsRealArithmetic()
    {
        const string source = "MODULE Real; VAR input, result: REAL; BEGIN result := input * 2.5 + 2.0; END Real.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("MOV r2, 0x40200000", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("FML r0, r0, r2", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("MOV r1, 0x40000000", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("FAD r0, r0, r1", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r13, 4]", Risc5Disassembler.Format(generator.Code[5]));
    }

    private static void FoldsRealConstantExpressions()
    {
        const string source = "MODULE Real; VAR result: REAL; BEGIN result := -(1.5 + 2.5 * 2.0); END Real.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal(2, generator.Code.Count);
        TestAssertions.Equal("MOV r0, 0xc0d00000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void FoldsUnaryRealLiteral()
    {
        const string source = "MODULE Real; VAR result: REAL; BEGIN result := -1.5; END Real.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal(2, generator.Code.Count);
        TestAssertions.Equal("MOV r0, 0xbfc00000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void EmitsIntegerRemainder()
    {
        const string source = "MODULE Arithmetic; VAR result: INTEGER; BEGIN result := 17 MOD 5; END Arithmetic.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("MOV r0, 0x0002", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
    }

    private static void FoldsUnaryIntegerConstantExpressions()
    {
        const string source = "MODULE Lower; VAR value: INTEGER; BEGIN value := -(2 + 3); END Lower.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(2, instructions.Length);
        TestAssertions.True(instructions[0].StartsWith("MOV "));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("SUB ")));
    }

    private static void EmitsSetLiteralAndOperations()
    {
        const string source = "MODULE Sets; VAR result: SET; BEGIN result := {1, 3} + {3, 5} - {1}; END Sets.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(1, instructions.Count(instruction => instruction.StartsWith("IOR ")));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction.StartsWith("ANN ")));
        TestAssertions.True(instructions.Any(instruction => instruction == "STR r0, [r13, 0]"));
    }

    private static void EmitsConstantControlFlowWithoutBranches()
    {
        const string source = "MODULE Constants; VAR value: INTEGER; BEGIN IF TRUE THEN value := 1 ELSE value := 2 END; WHILE FALSE DO value := 3 END; REPEAT value := value + 1 UNTIL TRUE END Constants.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("B")));
        TestAssertions.True(instructions.Any(instruction => instruction == "MOV r0, 0x0001"));
        TestAssertions.True(instructions.Any(instruction => instruction == "ADD r0, r0, 0x0001"));
    }

    private static void BranchesDirectlyOnIntegerComparisons()
    {
        const string source = "MODULE Branch; VAR value, result: INTEGER; BEGIN IF value < 3 THEN result := 1 END; END Branch.";
        var parseResult = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parseResult.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNLT ")));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0000"));
        TestAssertions.False(instructions.Any(instruction => instruction == "SUB r0, r0, r1"));
    }

    private static void BranchesDirectlyOnReversedIntegerComparisons()
    {
        const string source = "MODULE Branch; VAR value, result: INTEGER; BEGIN IF 3 < value THEN result := 1 END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r0, 0x0003"));
    }

    private static void BranchesWithTwoIntegerOperands()
    {
        const string source = "MODULE Branch; VAR left, right, result: INTEGER; BEGIN IF left < right THEN result := 1 END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("LDR r0, [r13, 0]", instructions[0]);
        TestAssertions.Equal("LDR r1, [r13, 4]", instructions[1]);
        TestAssertions.Equal("SUB r0, r0, r1", instructions[2]);
    }

    private static void BranchesWithConstantExpressionImmediate()
    {
        const string source = "MODULE Branch; VAR value, result: INTEGER; BEGIN IF value < (1 + 2) THEN result := 1 END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0003"));
    }

    private static void BranchesDirectlyOnSetMembership()
    {
        const string source = "MODULE SetBranch; VAR value: INTEGER; flags: SET; result: INTEGER; BEGIN IF value IN flags THEN result := 1 END; END SetBranch.";
        var parseResult = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parseResult.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BEQ ")));
        TestAssertions.True(instructions.Any(instruction => instruction == "AND r0, r0, r1"));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("BNEQ ")));
    }

    private static void BranchesDirectlyOnSetLiteralMembership()
    {
        const string source = "MODULE SetBranch; VAR value, result: INTEGER; BEGIN IF value IN {1, 3} THEN result := 1 END; END SetBranch.";
        var parseResult = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parseResult.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "AND r0, r0, 0x000a"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x000a"));
        TestAssertions.False(instructions.Any(instruction => instruction == "AND r0, r0, r1"));
    }

    private static void BranchesDirectlyOnRealComparisons()
    {
        const string source = "MODULE RealBranch; VAR value: REAL; result: INTEGER; BEGIN IF value < 1.5 THEN result := 1 END; END RealBranch.";
        var parseResult = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parseResult.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "FSB r0, r0, r1"));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNMI ")));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r0, 0x0000"));
    }

    private static void BranchesDirectlyOnRealAndSetPredicates()
    {
        const string source = "MODULE Branches; VAR left, right: REAL; value, other, result: INTEGER; flags, otherFlags: SET; BEGIN IF (left < 1.5) & (right >= 2.5) THEN result := 1 END; IF (value IN flags) OR (other IN otherFlags) THEN result := 2 END; END Branches.";
        var parseResult = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parseResult.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(2, instructions.Count(instruction => instruction.StartsWith("FSB ", StringComparison.Ordinal)));
        TestAssertions.Equal(2, instructions.Count(instruction => instruction == "AND r0, r0, r1"));
    }

    private static void EmitsRealComparisons()
    {
        const string source = "MODULE Real; VAR less, lessEqual, equal, notEqual, greaterEqual, greater: BOOLEAN; BEGIN less := 1.5 < 2.5; lessEqual := 1.5 <= 1.5; equal := 2.5 = 2.5; notEqual := 2.5 # 3.5; greaterEqual := 3.5 >= 2.5; greater := 3.5 > 2.5; END Real.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(6, instructions.Count(instruction => instruction.StartsWith("FSB ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BMI ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BEQ ")));
        TestAssertions.True(instructions.Any(instruction => instruction == "MOV r0, 0x0001"));
        TestAssertions.True(instructions.Any(instruction => instruction == "MOV r0, 0x0000"));
    }

    private static void EmitsIntegerComparisonsWithImmediateOperands()
    {
        const string source = "MODULE Compare; VAR value: INTEGER; less, equal: BOOLEAN; BEGIN less := value < 3; equal := value = 2; END Compare.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0002"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0002"));
    }

    private static void BranchesDirectlyOnIntegerConjunctions()
    {
        const string source = "MODULE Compound; VAR left, right, result: INTEGER; BEGIN IF (left = 1) & (right = 2) THEN result := 1; ELSE result := 0; END; END Compound.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(11, instructions.Length);
        TestAssertions.Equal(2, instructions.Count(instruction => instruction.StartsWith("BNEQ ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("AND ")));
    }

    private static void BranchesDirectlyOnIntegerDisjunctions()
    {
        const string source = "MODULE Compound; VAR left, right, result: INTEGER; BEGIN IF (left = 1) OR (right = 2) THEN result := 1; ELSE result := 0; END; END Compound.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(12, instructions.Length);
        TestAssertions.Equal(2, instructions.Count(instruction => instruction.StartsWith("BNEQ ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("IOR ")));
    }

    private static void BranchesNestedSideEffectFreeConditions()
    {
        const string source = "MODULE Branch; VAR a, b, c, result: INTEGER; BEGIN IF (a = 1) & ((b = 2) & (c = 3)) THEN result := 1; END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("AND ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("IOR ")));
        TestAssertions.True(instructions.Count(instruction => instruction.StartsWith("B")) >= 3);
    }

    private static void FoldsBooleanIdentitiesInBranches()
    {
        const string source = "MODULE Branch; VAR flag, result: BOOLEAN; BEGIN IF (flag & TRUE) OR FALSE THEN result := TRUE; END; END Branch.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("AND ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("IOR ")));
    }

    private static void EmitsReversedIntegerComparisonWithImmediateOperand()
    {
        const string source = "MODULE Compare; VAR value: INTEGER; result: BOOLEAN; BEGIN result := 3 < value; END Compare.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r0, 0x0003"));
    }

    private static void EmitsBooleanComparisonsWithoutBranches()
    {
        const string source = "MODULE BooleanCompare; VAR left, right, equal, different: BOOLEAN; BEGIN equal := left = right; different := left # right; END BooleanCompare.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(9, instructions.Length);
        TestAssertions.Equal(3, instructions.Count(instruction => instruction.StartsWith("XOR ", StringComparison.Ordinal)));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction == "XOR r0, r0, 0x0001"));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("B", StringComparison.Ordinal)));
    }

    private static void EmitsBooleanIdentitiesWithoutLogicalInstructions()
    {
        const string source = "MODULE BooleanIdentity; VAR flag, first, second, third, fourth: BOOLEAN; BEGIN first := flag & TRUE; second := flag OR FALSE; third := flag & FALSE; fourth := flag OR TRUE; END BooleanIdentity.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("AND ", StringComparison.Ordinal)));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("IOR ", StringComparison.Ordinal)));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction == "MOV r0, 0x0000"));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction == "MOV r0, 0x0001"));
    }

    private static void EmitsBooleanLiteralComparisonsWithoutTemporaries()
    {
        const string source = "MODULE BooleanLiteralCompare; VAR flag, first, second, third, fourth: BOOLEAN; BEGIN first := flag = TRUE; second := flag # FALSE; third := flag = FALSE; fourth := flag # TRUE; END BooleanLiteralCompare.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal(2, instructions.Count(instruction => instruction == "XOR r0, r0, 0x0001"));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("XOR r0, r0, r1", StringComparison.Ordinal)));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0000"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0001"));
    }

    private static void FoldsBooleanUnaryLiterals()
    {
        const string source = "MODULE BooleanUnary; VAR result: BOOLEAN; BEGIN result := ~TRUE; END BooleanUnary.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("MOV r0, 0x0000", instructions[0]);
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("XOR ", StringComparison.Ordinal)));
    }

    private static void FoldsBooleanConstantExpressions()
    {
        const string source = "MODULE Boolean; VAR result: BOOLEAN; BEGIN result := TRUE & (FALSE OR TRUE); END Boolean.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal(2, generator.Code.Count);
        TestAssertions.Equal("MOV r0, 0x0001", Risc5Disassembler.Format(generator.Code[0]));
    }

    private static void EmitsPointerValues()
    {
        const string source = "MODULE Pointers; TYPE Node = RECORD value: INTEGER; END; IntRef = POINTER TO Node; VAR first, second: IntRef; BEGIN first := NIL; second := first; END Pointers.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        TestAssertions.Equal("MOV r0, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("STR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r13, 4]", Risc5Disassembler.Format(generator.Code[3]));
    }

    private static void EmitsPointerDereference()
    {
        const string source = "MODULE Pointers; TYPE Node = RECORD value: INTEGER; END; IntRef = POINTER TO Node; VAR pointer: IntRef; BEGIN pointer^.value := 1; END Pointers.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("MOV r0, 0x0001", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("LDR r1, [r13, 0]", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[2]));
    }

    private static void EmitsForLoop()
    {
        const string source = "MODULE Loops; VAR index, total: INTEGER; BEGIN FOR index := 0 TO 3 BY 1 DO total := total + index; END; END Loops.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("MOV r0, 0x0000", instructions[0]);
        TestAssertions.Equal("STR r0, [r13, 0]", instructions[1]);
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNLE ")));
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0003"));
        TestAssertions.True(instructions.Contains("ADD r0, r0, 0x0001"));
        TestAssertions.False(instructions.Contains("MOV r2, 0x0001"));
        TestAssertions.False(instructions.Contains("MOV r3, 0x0000"));
    }

    private static void EmitsDescendingForLoop()
    {
        const string source = "MODULE Loops; VAR index, total: INTEGER; BEGIN FOR index := 3 TO 0 BY -1 DO total := total + index; END; END Loops.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNLT ")));
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0000"));
        TestAssertions.True(instructions.Contains("ADD r0, r0, -1"));
        TestAssertions.False(instructions.Contains("MOV r2, 0x0001"));
        TestAssertions.False(instructions.Contains("MOV r3, 0x0000"));
    }

    private static void EmitsCaseStatement()
    {
        const string source = "MODULE Cases; VAR value, result: INTEGER; BEGIN CASE value OF 0: result := 1 | 1, 2: result := 2 ELSE result := 3 END; END Cases.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("LDR r0, [r13, 0]", instructions[0]);
        TestAssertions.True(instructions.Count(instruction => instruction.StartsWith("BEQ ")) >= 3);
        TestAssertions.True(instructions.Contains("SUB r2, r0, 0x0000"));
        TestAssertions.True(instructions.Contains("SUB r2, r0, 0x0001"));
        TestAssertions.False(instructions.Contains("MOV r1, 0x0000"));
        TestAssertions.True(instructions.Contains("MOV r0, 0x0003"));
    }

    private static void EmitsCaseRange()
    {
        const string source = "MODULE Cases; VAR value, result: INTEGER; BEGIN CASE value OF 1..3: result := 1 ELSE result := 0 END; END Cases.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BLT ")));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BLE ")));
        TestAssertions.True(instructions.Contains("SUB r2, r0, 0x0001"));
        TestAssertions.True(instructions.Contains("SUB r2, r0, 0x0003"));
    }

    private static void EmitsSetMembership()
    {
        const string source = "MODULE Sets; VAR value: INTEGER; flags: SET; result: BOOLEAN; BEGIN result := value IN flags; END Sets.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("LDR r0, [r13, 0]", instructions[0]);
        TestAssertions.Equal("LDR r1, [r13, 4]", instructions[1]);
        TestAssertions.Equal("AND r0, r0, r1", instructions[2]);
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("BNEQ ")));
        TestAssertions.Equal("STR.B r0, [r13, 8]", instructions[^1]);
    }

    private static void EmitsSetLiteralMembershipWithImmediate()
    {
        const string source = "MODULE SetLiteral; VAR value: INTEGER; result: BOOLEAN; BEGIN result := value IN {1, 3}; END SetLiteral.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("LDR r0, [r13, 0]", instructions[0]);
        TestAssertions.True(instructions.Any(instruction => instruction == "AND r0, r0, 0x000a"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x000a"));
        TestAssertions.False(instructions.Any(instruction => instruction == "AND r0, r0, r1"));
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
        TestAssertions.Equal("ADD r0, r0, 0x0001", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[5]));
    }

    private static void EmitsConstantAtLastExpressionRegister()
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

        TestAssertions.Equal("MOV r11, 0x0005", Risc5Disassembler.Format(generator.Code[0]));
    }

    private static void EmitsAggregateRecordCopy()
    {
        const string source = "MODULE Records; TYPE Pair = RECORD left, right: INTEGER; END; VAR source, target: Pair; BEGIN target := source; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r2, r13, 0x0008", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r2, 0]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("LDR r0, [r1, 4]", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r2, 4]", Risc5Disassembler.Format(generator.Code[5]));
    }

    private static void EmitsFieldAddressThroughDedicatedEmitter()
    {
        const string source = "MODULE Records; TYPE Point = RECORD x, y: INTEGER; END; VAR point: Point; BEGIN point.x := 1; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerAddressEmitter(generator).EmitAddress(assignment.Target, 0);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
    }

    private static void EmitsArrayIndexLoadAndStore()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 4 OF INTEGER; VAR values: Values; index: INTEGER; BEGIN values[index] := values[index] + 1; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("LDR r1, [r13, 16]", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LSL r1, r1, 0x0002", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("ADD r0, r0, r1", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("LDR r0, [r0, 0]", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("ADD r0, r0, 0x0001", Risc5Disassembler.Format(generator.Code[5]));
        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[6]));
        TestAssertions.Equal("LDR r2, [r13, 16]", Risc5Disassembler.Format(generator.Code[7]));
        TestAssertions.Equal("LSL r2, r2, 0x0002", Risc5Disassembler.Format(generator.Code[8]));
        TestAssertions.Equal("ADD r1, r1, r2", Risc5Disassembler.Format(generator.Code[9]));
        TestAssertions.Equal("STR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[10]));
    }

    private static void EmitsNonPowerOfTwoArrayIndexWithMultiply()
    {
        const string source = "MODULE Arrays; TYPE Item = RECORD first, second, third: INTEGER; END; Values = ARRAY 2 OF Item; VAR values: Values; index: INTEGER; BEGIN values[index].third := 1; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("MOV ")
            && instruction.EndsWith("0x000c", StringComparison.Ordinal)));
        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("MUL ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("LSL ")));
    }

    private static void EmitsConstantExpressionArrayIndexAddress()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 8 OF INTEGER; VAR values: Values; BEGIN values[1 + 2] := 7; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "ADD r1, r1, 0x000c"));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("LSL ")));
    }

    private static void EmitsAggregateFieldAddress()
    {
        const string source = "MODULE Records; TYPE Holder = RECORD values: ARRAY 2 OF INTEGER; END; VAR holder: Holder; BEGIN END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var holder = result.ModuleScope!.Objects.Single(obj => obj.Name == "holder");
        var field = new OberonCompilerFieldExpression(
            new OberonCompilerNameExpression("holder", holder),
            holder.Type!.Fields[0]);

        var generator = new OberonCompilerCodeGenerator();
        new OberonCompilerExpressionEmitter(generator).EmitExpression(field);

        TestAssertions.Equal("ADD r0, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
    }

    private static void EmitsAggregateArrayElementAddress()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 2 OF ARRAY 2 OF INTEGER; VAR values, target: Values; BEGIN target := values[0]; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();
        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        TestAssertions.Equal("ADD r1, r13, 0x0000", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r2, r13, 0x0010", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r2, 0]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("LDR r0, [r1, 4]", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r2, 4]", Risc5Disassembler.Format(generator.Code[5]));
        TestAssertions.Equal("LDR r0, [r1, 8]", Risc5Disassembler.Format(generator.Code[6]));
        TestAssertions.Equal("STR r0, [r2, 8]", Risc5Disassembler.Format(generator.Code[7]));
        TestAssertions.Equal("LDR r0, [r1, 12]", Risc5Disassembler.Format(generator.Code[8]));
        TestAssertions.Equal("STR r0, [r2, 12]", Risc5Disassembler.Format(generator.Code[9]));
    }

    private static void EmitsAggregatePointerDereferenceAddress()
    {
        const string source = "MODULE Pointers; TYPE Values = ARRAY 2 OF INTEGER; Ref = POINTER TO Values; VAR value: Ref; target: Values; BEGIN target := value^; END Pointers.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        TestAssertions.Equal("LDR r1, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r2, r13, 0x0004", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.Equal("LDR r0, [r1, 0]", Risc5Disassembler.Format(generator.Code[2]));
        TestAssertions.Equal("STR r0, [r2, 0]", Risc5Disassembler.Format(generator.Code[3]));
        TestAssertions.Equal("LDR r0, [r1, 4]", Risc5Disassembler.Format(generator.Code[4]));
        TestAssertions.Equal("STR r0, [r2, 4]", Risc5Disassembler.Format(generator.Code[5]));
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

        TestAssertions.Equal(1, writer.Calls.Count);
        TestAssertions.Equal("MOV r4, 0x0005", writer.Calls[0]);
    }

    private static void EmitsLargeIntegerLiteralsThroughInstructionWriter()
    {
        var symbols = new OberonCompilerSymbols();
        var expression = new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 0x1_0000);
        var writer = new RecordingInstructionWriter();

        new OberonCompilerExpressionEmitter(writer).EmitExpression(expression, destination: 4);

        TestAssertions.Equal(1, writer.Calls.Count);
        TestAssertions.Equal("MOV r4, 0x10000", writer.Calls[0]);
    }

    private static void EmitsImmediateOperandsForConstantExpressions()
    {
        const string source = "MODULE ImmediateConstant; VAR value, result: INTEGER; BEGIN result := value + (1 + 2); END ImmediateConstant.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.Equal("LDR r0, [r13, 0]", instructions[0]);
        TestAssertions.Equal("ADD r0, r0, 0x0003", instructions[1]);
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0003"));
    }

    private static void EmitsImmediateForConstantLeftArithmetic()
    {
        const string source = "MODULE Arithmetic; VAR value, result: INTEGER; BEGIN result := 2 + value; END Arithmetic.";
        var result = new OberonCompilerParser(source).ParseModule();
        var assignment = (OberonCompilerAssignmentStatement)result.Body!.Statements[0];
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitAssignment(assignment);

        TestAssertions.Equal("LDR r0, [r13, 0]", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal("ADD r0, r0, 0x0002", Risc5Disassembler.Format(generator.Code[1]));
        TestAssertions.False(generator.Code.Select(Risc5Disassembler.Format)
            .Any(instruction => instruction.Contains("r1,", StringComparison.Ordinal)));
    }

    private static void EmitsComparisonImmediateForConstantExpressions()
    {
        const string source = "MODULE ComparisonImmediateConstant; VAR value: INTEGER; result: BOOLEAN; BEGIN result := value < (1 + 2); END ComparisonImmediateConstant.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Any(instruction => instruction == "SUB r0, r0, 0x0003"));
        TestAssertions.False(instructions.Any(instruction => instruction == "MOV r1, 0x0003"));
    }

    private static void ValidatesImmediateBoundaryOptimizations()
    {
        var lower = EmitIntegerAssignment("value + (-32768)");
        var upper = EmitIntegerAssignment("value + 65535");
        var outside = EmitIntegerAssignment("value + 65536");

        TestAssertions.Equal(3, lower.Length);
        TestAssertions.Equal(3, upper.Length);
        TestAssertions.True(outside.Any(instruction => instruction.StartsWith("MOV r1,")));
        TestAssertions.True(outside.Any(instruction => instruction == "ADD r0, r0, r1"));
    }

    private static string[] EmitIntegerAssignment(string expression)
    {
        var source = $"MODULE Immediate; VAR value, result: INTEGER; BEGIN result := {expression}; END Immediate.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();
        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);
        return generator.Code.Select(Risc5Disassembler.Format).ToArray();
    }

    private static void FoldsIntegerConstantExpressions()
    {
        var symbols = new OberonCompilerSymbols();
        var expression = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 2),
            new OberonCompilerBinaryExpression(
                OberonCompilerTokenKind.Multiply,
                new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 3),
                new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 4)));
        var writer = new RecordingInstructionWriter();

        new OberonCompilerExpressionEmitter(writer).EmitExpression(expression, destination: 4);

        TestAssertions.Equal(1, writer.Calls.Count);
        TestAssertions.Equal("MOV r4, 0x000e", writer.Calls[0]);
    }

    private static void EliminatesIntegerIdentityOperations()
    {
        var symbols = new OberonCompilerSymbols();
        var operations = new[]
        {
            (OberonCompilerTokenKind.Plus, 0L),
            (OberonCompilerTokenKind.Minus, 0L),
            (OberonCompilerTokenKind.Multiply, 1L),
            (OberonCompilerTokenKind.Divide, 1L)
        };

        foreach (var (operation, value) in operations)
        {
            var expression = new OberonCompilerBinaryExpression(
                operation,
                new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 9),
                new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, value));
            var writer = new RecordingInstructionWriter();

            new OberonCompilerExpressionEmitter(writer).EmitExpression(expression, destination: 4);

            TestAssertions.Equal(1, writer.Calls.Count);
            TestAssertions.Equal("MOV r4, 0x0009", writer.Calls[0]);
        }
    }

    private static void EliminatesIntegerBitwiseIdentityOperations()
    {
        const string source = "MODULE IntegerBitwiseIdentity; VAR value, first, second, third, fourth: INTEGER; BEGIN first := value OR 0; second := value & (-1); third := value & 0; fourth := value OR (-1); END IntegerBitwiseIdentity.";
        var result = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();

        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(result.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("AND ", StringComparison.Ordinal)));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("IOR ", StringComparison.Ordinal)));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction == "MOV r0, 0x0000"));
        TestAssertions.Equal(1, instructions.Count(instruction => instruction == "MOV r0, -1"));
    }

    private sealed class RecordingInstructionWriter : IOberonCompilerInstructionWriter
    {
        public List<string> Calls { get; } = [];

        public void EmitRegister(AluOperation operation, int destination, int left, int right, bool carry = false) =>
            Calls.Add($"{operation} r{destination}, r{left}, r{right}");

        public void EmitMoveImmediate(int destination, int value) =>
            Calls.Add($"MOV r{destination}, 0x{value:x4}");

        public void EmitMoveFromHigh(int destination) => Calls.Add($"MOV r{destination}, H");

        public void EmitImmediate(AluOperation operation, int destination, int left, int immediate) =>
            Calls.Add($"{operation} r{destination}, r{left}, 0x{immediate:x4}");

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
