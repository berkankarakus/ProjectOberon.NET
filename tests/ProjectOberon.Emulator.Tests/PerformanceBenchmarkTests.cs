using System.Diagnostics;
using ProjectOberon.Compiler;
using ProjectOberon.Emulator;

internal static class PerformanceBenchmarkTests
{
    public static void Run()
    {
        var iterations = ReadIterations();
        Console.WriteLine($"benchmark iterations={iterations}");
        var results = new[]
        {
            Benchmark("memory read/write", iterations, BenchmarkMemory),
            Benchmark("memory read/write with statistics", iterations, BenchmarkMemoryWithStatistics),
            Benchmark("cpu step", iterations, BenchmarkCpu),
            Benchmark("machine step", iterations, BenchmarkMachine),
            Benchmark("machine run with progress", Math.Max(1, iterations / 10), BenchmarkMachineWithProgress),
            Benchmark("host call dispatch", iterations, BenchmarkHostCallDispatch),
            Benchmark("display pattern copy", Math.Max(1, iterations / 10), BenchmarkDisplayPattern),
            Benchmark("compiler emit", Math.Max(1, iterations / 100), BenchmarkCompiler)
        };

        var outputPath = Environment.GetEnvironmentVariable("PROJECT_OBERON_BENCHMARK_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(
                fullPath,
                System.Text.Json.JsonSerializer.Serialize(results,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"benchmark output={fullPath}");
        }

        var baselinePath = Environment.GetEnvironmentVariable("PROJECT_OBERON_BENCHMARK_BASELINE");
        if (!string.IsNullOrWhiteSpace(baselinePath))
        {
            CompareBaseline(results, Path.GetFullPath(baselinePath));
        }
    }

    private static BenchmarkResult Benchmark(string name, int iterations, Action<int> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        action(iterations);
        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        var operationsPerSecond = iterations / Math.Max(stopwatch.Elapsed.TotalSeconds, double.Epsilon);
        Console.WriteLine(
            $"{name}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, "
            + $"{operationsPerSecond:F0} ops/s, allocated={allocatedBytes} bytes");
        return new BenchmarkResult(
            name,
            iterations,
            stopwatch.Elapsed.TotalMilliseconds,
            operationsPerSecond,
            allocatedBytes);
    }

    private static void BenchmarkMemory(int iterations)
    {
        var memory = new Memory();
        RunMemoryLoop(memory, iterations);
    }

    private static void BenchmarkMemoryWithStatistics(int iterations)
    {
        var memory = new Memory { CollectStatistics = true };
        RunMemoryLoop(memory, iterations);
        GC.KeepAlive(memory.GetStatistics());
    }

    private static void RunMemoryLoop(Memory memory, int iterations)
    {
        uint value = 0;
        for (var index = 0; index < iterations; index++)
        {
            memory.Write32((uint)((index * 4) & 0x3fff), (uint)index);
            value ^= memory.Read32((uint)((index * 4) & 0x3fff));
        }

        GC.KeepAlive(value);
    }

    private static void BenchmarkCpu(int iterations)
    {
        var memory = new Memory();
        var rom = new uint[Memory.RomWords];
        Array.Fill(rom, TestFixtures.Alu(AluOperation.Add, 0, 0, 0));
        memory.LoadRomWords(rom);
        var cpu = new Risc5Cpu(memory);
        for (var index = 0; index < iterations; index++)
        {
            cpu.Step();
        }

        GC.KeepAlive(cpu.Steps);
    }

    private static void BenchmarkMachine(int iterations)
    {
        using var machine = CreateBenchmarkMachine();
        machine.Run((ulong)iterations);
        GC.KeepAlive(machine.Cpu.Steps);
    }

    private static void BenchmarkMachineWithProgress(int iterations)
    {
        using var machine = CreateBenchmarkMachine();
        machine.Run(
            (ulong)iterations,
            static _ => { },
            progressInterval: (ulong)Math.Max(1, iterations / 4));
        GC.KeepAlive(machine.Cpu.Steps);
    }

    private static void BenchmarkHostCallDispatch(int iterations)
    {
        using var machine = new Machine();
        var address = machine.Cpu.PcByte;
        machine.HostCalls.Register(address, static context =>
            context.Cpu.SetProgramCounter(context.Cpu.PcByte));
        machine.Run((ulong)iterations);
        GC.KeepAlive(machine.Cpu.Steps);
    }

    private static void BenchmarkDisplayPattern(int iterations)
    {
        var memory = new Memory();
        memory.Write8(0x100, 32);
        memory.Write8(0x101, 1);
        memory.Write32(0x102, uint.MaxValue);
        var display = new OberonDisplay(memory);
        for (var index = 0; index < iterations; index++)
        {
            display.CopyPattern(Display.White, 0x100, 0, index % Display.Height, Display.Paint);
        }

        GC.KeepAlive(memory);
    }

    private static Machine CreateBenchmarkMachine()
    {
        var machine = new Machine();
        var rom = new uint[Memory.RomWords];
        Array.Fill(rom, TestFixtures.Alu(AluOperation.Add, 0, 0, 0));
        machine.Memory.LoadRomWords(rom);
        return machine;
    }

    private static void BenchmarkCompiler(int iterations)
    {
        const string source =
            "MODULE Benchmark; VAR value: INTEGER; BEGIN value := (7 + 5) * 3 END Benchmark.";
        for (var index = 0; index < iterations; index++)
        {
            var parsed = new OberonCompilerParser(source).ParseModule();
            _ = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(parsed);
        }
    }

    private static int ReadIterations()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("PROJECT_OBERON_BENCHMARK_ITERATIONS"), out var iterations)
            && iterations > 0)
        {
            return iterations;
        }

        return 1_000_000;
    }

    private static void CompareBaseline(
        IReadOnlyList<BenchmarkResult> current,
        string baselinePath)
    {
        if (!File.Exists(baselinePath))
        {
            throw new FileNotFoundException("benchmark baseline was not found", baselinePath);
        }

        var baseline = System.Text.Json.JsonSerializer.Deserialize<BenchmarkResult[]>(
            File.ReadAllText(baselinePath)) ?? [];
        var baselineByName = baseline.ToDictionary(result => result.Name, StringComparer.Ordinal);
        var factor = 1.25;
        if (double.TryParse(
                Environment.GetEnvironmentVariable("PROJECT_OBERON_BENCHMARK_TOLERANCE"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var configuredFactor)
            && double.IsFinite(configuredFactor)
            && configuredFactor >= 1)
        {
            factor = configuredFactor;
        }

        foreach (var result in current)
        {
            if (!baselineByName.TryGetValue(result.Name, out var expected))
            {
                throw new InvalidOperationException($"benchmark baseline is missing '{result.Name}'");
            }

            if (result.ElapsedMilliseconds > expected.ElapsedMilliseconds * factor
                || result.OperationsPerSecond < expected.OperationsPerSecond / factor
                || result.AllocatedBytes > expected.AllocatedBytes * factor)
            {
                throw new InvalidOperationException(
                    $"benchmark regression in '{result.Name}': "
                    + $"current={result.ElapsedMilliseconds:F2}ms/{result.OperationsPerSecond:F0}ops/s/{result.AllocatedBytes}B, "
                    + $"baseline={expected.ElapsedMilliseconds:F2}ms/{expected.OperationsPerSecond:F0}ops/s/{expected.AllocatedBytes}B, "
                    + $"tolerance={factor:F2}");
            }
        }

        Console.WriteLine($"benchmark regression check passed (tolerance={factor:F2})");
    }

    private sealed record BenchmarkResult(
        string Name,
        int Iterations,
        double ElapsedMilliseconds,
        double OperationsPerSecond,
        long AllocatedBytes);
}
