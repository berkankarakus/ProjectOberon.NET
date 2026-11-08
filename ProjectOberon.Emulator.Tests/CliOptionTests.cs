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
            "--breakpoint", "0x00ff",
            "--boot-trace",
            "--boot-graph",
            "--start-module", "Oberon",
            "--command", "Start"]);

        TestAssertions.Equal("prom.mem", options.Rom);
        TestAssertions.Equal("work.img", options.Disk);
        TestAssertions.False(options.DiskWritable);
        TestAssertions.Equal(42UL, options.Steps);
        TestAssertions.Equal(3, options.Trace!.Value);
        TestAssertions.Equal(0xffu, options.Breakpoint!.Value);
        TestAssertions.True(options.BootTrace);
        TestAssertions.True(options.BootGraph);
        TestAssertions.Equal("Oberon", options.StartModule);
        TestAssertions.Equal("Start", options.Command);
    }

    private static void UsesSafeDefaults()
    {
        var options = Options.Parse([]);

        TestAssertions.Equal(1000UL, options.Steps);
        TestAssertions.True(options.DiskWritable);
        TestAssertions.False(options.Help);
        TestAssertions.True(options.Trace is null);
        TestAssertions.True(options.Breakpoint is null);
    }

    private static void RejectsOptionsThatRequireADisk()
    {
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--read-only"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--boot-trace"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--boot-graph"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--start-module", "Oberon"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--disk", "work.img", "--command", "Start"]));
    }

    private static void RejectsInvalidValues()
    {
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--steps", "x"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--trace", "-1"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--breakpoint", "not-hex"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--unknown"]));
        TestAssertions.Throws<ArgumentException>(() => Options.Parse(["--disk"]));
    }
}
