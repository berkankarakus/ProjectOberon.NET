using ProjectOberon.Emulator;

internal static class CliOptionTests
{
    public static void Run()
    {
        ParsesAllOptions();
        UsesSafeDefaults();
        RejectsOptionsThatRequireADisk();
        RejectsInvalidValues();
    }

    private static void ParsesAllOptions()
    {
        var options = Options.Parse([
            "--rom", "prom.mem",
            "--disk", "work.img",
            "--read-only",
            "--steps", "42",
            "--trace", "3",
            "--trace-history", "5",
            "--progress", "10",
            "--breakpoint", "0x00ff",
            "--boot-trace",
            "--boot-graph",
            "--start-module", "Oberon",
            "--boot",
            "--command", "Start"]);

        TestAssertions.Equal("prom.mem", options.Rom);
        TestAssertions.Equal("work.img", options.Disk);
        TestAssertions.False(options.DiskWritable);
        TestAssertions.Equal(42UL, options.Steps);
        TestAssertions.Equal(3, options.Trace!.Value);
        TestAssertions.Equal(5, options.TraceHistory!.Value);
        TestAssertions.Equal(10UL, options.ProgressInterval!.Value);
        TestAssertions.Equal(0xffu, options.Breakpoint!.Value);
        TestAssertions.True(options.BootTrace);
        TestAssertions.True(options.BootGraph);
        TestAssertions.Equal("Oberon", options.StartModule);
        TestAssertions.False(options.Initialize);
        TestAssertions.True(options.Boot);
        TestAssertions.Equal("Start", options.Command);
    }

    private static void UsesSafeDefaults()
    {
        var options = Options.Parse([]);

        TestAssertions.Equal(1000UL, options.Steps);
        TestAssertions.True(options.DiskWritable);
        TestAssertions.False(options.Help);
        TestAssertions.True(options.Trace is null);
        TestAssertions.True(options.TraceHistory is null);
        TestAssertions.True(options.ProgressInterval is null);
        TestAssertions.True(options.Breakpoint is null);
        TestAssertions.False(options.Initialize);
        TestAssertions.False(options.Boot);
        TestAssertions.True(Options.Parse(["-h", "--unknown"]).Help);
    }

    private static void RejectsOptionsThatRequireADisk()
    {
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--read-only"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--boot-trace"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--boot-graph"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--start-module", "Oberon"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--initialize"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--boot"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--initialize", "--boot", "--start-module", "Oberon", "--disk", "work.img"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--disk", "work.img", "--command", "Start"]));
    }

    private static void RejectsInvalidValues()
    {
        TestAssertions.Throws<ArgumentNullException>(() => Options.Parse(null!));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--steps", "x"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--trace", "-1"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--trace-history", "0"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--progress", "0"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--breakpoint", "not-hex"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--unknown"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--disk"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--rom", "first.mem", "--rom", "second.mem"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--disk", "first.img", "--disk", "second.img"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--steps", "1", "--steps", "2"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--trace", "1", "--trace", "2"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--trace-history", "1", "--trace-history", "2"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--progress", "1", "--progress", "2"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--breakpoint", "10", "--breakpoint", "20"]));
    }
}
