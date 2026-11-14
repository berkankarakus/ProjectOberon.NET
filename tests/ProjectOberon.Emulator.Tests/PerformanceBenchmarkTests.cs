using System.Diagnostics;
using ProjectOberon.Compiler;
using ProjectOberon.Emulator;

internal static class PerformanceBenchmarkTests
{
    public static void Run()
    {
        var iterations = ReadIterations();
        Console.WriteLine($"benchmark iterations={iterations}");
        Benchmark("memory read/write", iterations, BenchmarkMemory);
        Benchmark("cpu step", iterations, BenchmarkCpu);
        Benchmark("compiler emit", Math.Max(1, iterations / 100), BenchmarkCompiler);
    }

    private static void Benchmark(string name, int iterations, Action<int> action)
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
    }

    private static void BenchmarkMemory(int iterations)
    {
        var memory = new Memory();
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
        var cpu = new Risc5Cpu(memory);
        for (var index = 0; index < iterations; index++)
        {
            cpu.Step();
        }

        GC.KeepAlive(cpu.Steps);
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
}
