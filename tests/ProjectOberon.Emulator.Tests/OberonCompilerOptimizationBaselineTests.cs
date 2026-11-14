using ProjectOberon.Emulator;

internal static class OberonCompilerOptimizationBaselineTests
{
    public static void Run()
    {
        MeasuresArithmeticFixture();
        MeasuresConstantArithmeticFixture();
        MeasuresRealConstantFixture();
        MeasuresBooleanComparisonFixture();
        MeasuresBooleanBranchFixture();
        MeasuresNegatedBooleanBranchFixture();
        MeasuresNegatedIntegerComparisonBranchFixture();
        MeasuresCompoundComparisonBranchFixture();
        MeasuresDisjunctionComparisonBranchFixture();
        MeasuresLoopFixture();
        MeasuresForLoopFixture();
        MeasuresAggregateCopyFixture();
        MatchesImmediateOptimizationGoldenFixture();
    }

    private static void MeasuresArithmeticFixture()
    {
        const string source =
            "MODULE Arithmetic; VAR left, right, result: INTEGER; BEGIN left := 7; right := 3; result := left + right * 2 END Arithmetic.";
        var module = Compile("Arithmetic", source);
        var result = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "result");
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 13,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 9, rscBytes: 99, runtimeSteps: 9);
    }

    private static void MeasuresConstantArithmeticFixture()
    {
        const string source =
            "MODULE ConstantArithmetic; VAR result: INTEGER; BEGIN result := 2 + 3 * 4 END ConstantArithmetic.";
        var module = Compile("ConstantArithmetic", source);
        var result = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "result");
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 14,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 2, rscBytes: 79, runtimeSteps: 2);
    }

    private static void MeasuresRealConstantFixture()
    {
        const string source =
            "MODULE RealConstant; VAR result: REAL; BEGIN result := 1.5 + 2.5 * 2.0 END RealConstant.";
        var module = Compile("RealConstant", source);
        var result = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "result");
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value))
                == 0x40d00000u,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 2, rscBytes: 73, runtimeSteps: 2);
    }

    private static void MeasuresBooleanComparisonFixture()
    {
        const string source =
            "MODULE BooleanCompare; VAR left, right, result: BOOLEAN; BEGIN result := left = right END BooleanCompare.";
        var module = Compile("BooleanCompare", source);
        var result = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "result");
        var steps = RunUntil(
            module,
            () => module.Memory.Read8(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 5, rscBytes: 87, runtimeSteps: 5);
    }

    private static void MeasuresBooleanBranchFixture()
    {
        const string source =
            "MODULE BooleanBranch; VAR flag: BOOLEAN; result: INTEGER; BEGIN IF flag THEN result := 1; END; END BooleanBranch.";
        var module = Compile("BooleanBranch", source);
        var flag = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "flag");
        var result = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "result");
        module.Memory.Write8(module.Image.DataAddress + checked((uint)flag.Value), 1);
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 6, rscBytes: 90, runtimeSteps: 5);
    }

    private static void MeasuresNegatedBooleanBranchFixture()
    {
        const string source =
            "MODULE NegatedBooleanBranch; VAR flag: BOOLEAN; result: INTEGER; BEGIN IF ~flag THEN result := 1; END; END NegatedBooleanBranch.";
        var module = Compile("NegatedBooleanBranch", source);
        var flag = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "flag");
        var result = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "result");
        module.Memory.Write8(module.Image.DataAddress + checked((uint)flag.Value), 0);
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 6, rscBytes: 97, runtimeSteps: 5);
    }

    private static void MeasuresNegatedIntegerComparisonBranchFixture()
    {
        const string source =
            "MODULE NegatedIntegerBranch; VAR value, result: INTEGER; BEGIN IF ~(value = 1) THEN result := 1; END; END NegatedIntegerBranch.";
        var module = Compile("NegatedIntegerBranch", source);
        var value = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "value");
        var result = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "result");
        module.Memory.Write32(module.Image.DataAddress + checked((uint)value.Value), 0);
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 64);

        AssertBaseline(module, steps, codeWords: 6, rscBytes: 97, runtimeSteps: 5);
    }

    private static void MeasuresCompoundComparisonBranchFixture()
    {
        const string source =
            "MODULE CompoundBranch; VAR left, right, result: INTEGER; BEGIN IF (left = 1) & (right = 2) THEN result := 1; ELSE result := 0; END; END CompoundBranch.";
        var module = Compile("CompoundBranch", source);
        var left = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "left");
        var right = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "right");
        var result = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "result");
        module.Memory.Write32(module.Image.DataAddress + checked((uint)left.Value), 1);
        module.Memory.Write32(module.Image.DataAddress + checked((uint)right.Value), 2);
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 128);

        AssertBaseline(module, steps, codeWords: 11, rscBytes: 111, runtimeSteps: 8);
    }

    private static void MeasuresDisjunctionComparisonBranchFixture()
    {
        const string source =
            "MODULE DisjunctionBranch; VAR left, right, result: INTEGER; BEGIN IF (left = 1) OR (right = 2) THEN result := 1; ELSE result := 0; END; END DisjunctionBranch.";
        var module = Compile("DisjunctionBranch", source);
        var left = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "left");
        var right = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "right");
        var result = module.ParseResult.ModuleScope.Objects.Single(symbol => symbol.Name == "result");
        module.Memory.Write32(module.Image.DataAddress + checked((uint)left.Value), 0);
        module.Memory.Write32(module.Image.DataAddress + checked((uint)right.Value), 2);
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)result.Value)) == 1,
            maxSteps: 128);

        AssertBaseline(module, steps, codeWords: 12, rscBytes: 118, runtimeSteps: 8);
    }

    private static void MeasuresLoopFixture()
    {
        const string source =
            "MODULE Loop; VAR index, total: INTEGER; BEGIN index := 0; total := 0; WHILE index < 8 DO total := total + index; index := index + 1 END END Loop.";
        var module = Compile("Loop", source);
        var total = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "total");
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)total.Value)) == 28,
            maxSteps: 256);

        AssertBaseline(module, steps, codeWords: 15, rscBytes: 117, runtimeSteps: 88);
    }

    private static void MeasuresForLoopFixture()
    {
        const string source =
            "MODULE ForLoop; VAR index, total: INTEGER; BEGIN total := 0; FOR index := 0 TO 7 BY 1 DO total := total + index END END ForLoop.";
        var module = Compile("ForLoop", source);
        var total = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "total");
        var steps = RunUntil(
            module,
            () => module.Memory.Read32(module.Image.DataAddress + checked((uint)total.Value)) == 28,
            maxSteps: 256);

        AssertBaseline(module, steps, codeWords: 15, rscBytes: 120, runtimeSteps: 88);
    }

    private static void MeasuresAggregateCopyFixture()
    {
        const string source =
            "MODULE Aggregate; TYPE Values = ARRAY 4 OF INTEGER; VAR source, target: Values; BEGIN target := source END Aggregate.";
        var module = Compile("Aggregate", source);
        var sourceSymbol = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "source");
        var targetSymbol = module.ParseResult.ModuleScope!.Objects.Single(symbol => symbol.Name == "target");
        var sourceAddress = module.Image.DataAddress + checked((uint)sourceSymbol.Value);
        var targetAddress = module.Image.DataAddress + checked((uint)targetSymbol.Value);
        for (var index = 0; index < 4; index++)
        {
            module.Memory.Write32(sourceAddress + checked((uint)(index * sizeof(int))), (uint)(index + 11));
        }

        var steps = RunUntil(
            module,
            () => Enumerable.Range(0, 4).All(index =>
                module.Memory.Read32(targetAddress + checked((uint)(index * sizeof(int)))) == index + 11),
            maxSteps: 256);

        AssertBaseline(module, steps, codeWords: 10, rscBytes: 102, runtimeSteps: 10);
    }

    private static void MatchesImmediateOptimizationGoldenFixture()
    {
        const string source =
            "MODULE ImmediateGolden; VAR value, result: INTEGER; BEGIN result := value + (1 + 2) END ImmediateGolden.";
        var parsed = new OberonCompilerParser(source).ParseModule();
        var generator = new OberonCompilerCodeGenerator();
        new OberonCompilerStatementEmitter(generator).EmitStatementSequence(parsed.Body!);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.SequenceEqual([
            "LDR r0, [r13, 0]",
            "ADD r0, r0, 0x0003",
            "STR r0, [r13, 4]"
        ]));
    }

    private static CompiledModule Compile(string moduleName, string source)
    {
        var parseResult = new OberonCompilerParser(source).ParseModule();
        TestAssertions.True(parseResult.Succeeded);
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator())
            .EmitRsc(parseResult);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == moduleName ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        var image = runtime.Start(moduleName);
        return new CompiledModule(parseResult, definition, bytes, memory, cpu, image);
    }

    private static int RunUntil(
        CompiledModule module,
        Func<bool> condition,
        int maxSteps)
    {
        for (var step = 0; step < maxSteps; step++)
        {
            if (condition())
            {
                return step;
            }

            module.Cpu.Step();
        }

        if (condition())
        {
            return maxSteps;
        }

        throw new InvalidOperationException($"compiler fixture did not complete in {maxSteps} steps");
    }

    private static void AssertBaseline(
        CompiledModule module,
        int steps,
        int codeWords,
        int rscBytes,
        int runtimeSteps)
    {
        TestAssertions.Equal(codeWords, module.Definition.CodeWords);
        TestAssertions.Equal(rscBytes, module.Bytes.Length);
        TestAssertions.Equal(runtimeSteps, steps);
    }

    private sealed record CompiledModule(
        OberonCompilerModuleParseResult ParseResult,
        RscModule Definition,
        byte[] Bytes,
        Memory Memory,
        Risc5Cpu Cpu,
        OberonModuleImage Image);
}
