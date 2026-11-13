using ProjectOberon.Emulator;

internal static class OberonCompilerOptimizationBaselineTests
{
    public static void Run()
    {
        MeasuresArithmeticFixture();
        MeasuresLoopFixture();
        MeasuresAggregateCopyFixture();
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

        AssertBaseline(module, steps, codeWords: 22, rscBytes: 145, runtimeSteps: 128);
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
