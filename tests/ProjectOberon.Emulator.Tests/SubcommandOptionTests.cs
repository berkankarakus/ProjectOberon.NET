internal static class SubcommandOptionTests
{
    public static void Run()
    {
        ParsesFileSystemOptions();
        ParsesFileSystemCommands();
        HandlesFileSystemHelp();
        RejectsInvalidFileSystemOptions();
        ParsesRscOptions();
        HandlesRscHelp();
        RejectsInvalidRscOptions();
    }

    private static void ParsesFileSystemOptions()
    {
        var options = FsOptions.Parse(["--disk", "work.img", "--prefix", "Ober"]);

        TestAssertions.Equal("work.img", options.Disk);
        TestAssertions.Equal("Ober", options.Prefix);
        TestAssertions.False(options.Writable);
        TestAssertions.Equal(FsCommand.List, options.Command);
        TestAssertions.False(options.Help);
    }

    private static void ParsesFileSystemCommands()
    {
        var cat = FsOptions.Parse(["--disk", "work.img", "--cat", "System.rsc"]);
        TestAssertions.Equal(FsCommand.Cat, cat.Command);
        TestAssertions.Equal("System.rsc", cat.Name);

        var put = FsOptions.Parse(["--disk", "work.img", "--put", "Demo.Mod", "demo.mod", "--writable"]);
        TestAssertions.Equal(FsCommand.Put, put.Command);
        TestAssertions.Equal("Demo.Mod", put.Name);
        TestAssertions.Equal("demo.mod", put.Path);
        TestAssertions.True(put.Writable);

        var rename = FsOptions.Parse(["--disk", "work.img", "--rename", "Old.Mod", "New.Mod", "--writable"]);
        TestAssertions.Equal(FsCommand.Rename, rename.Command);
        TestAssertions.Equal("Old.Mod", rename.Name);
        TestAssertions.Equal("New.Mod", rename.NewName);

        var validate = FsOptions.Parse(["--disk", "work.img", "--validate"]);
        TestAssertions.Equal(FsCommand.Validate, validate.Command);
        TestAssertions.False(validate.Writable);
    }

    private static void HandlesFileSystemHelp()
    {
        TestAssertions.True(FsOptions.Parse([]).Help);
        TestAssertions.True(FsOptions.Parse(["--help", "--unknown"]).Help);
        TestAssertions.True(FsOptions.Parse(["-h", "--unknown"]).Help);
    }

    private static void RejectsInvalidFileSystemOptions()
    {
        TestAssertions.Throws<ArgumentNullException>(() => FsOptions.Parse(null!));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--prefix", "Ober"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk", "one.img", "--disk", "two.img"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk", "work.img", "--prefix", "A", "--prefix", "B"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--unknown"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk", "work.img", "--delete", "Demo.Mod"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk", "work.img", "--cat", "A", "--delete", "B"]));
        TestAssertions.Throws<ArgumentException>(() => FsOptions.Parse(["--disk", "work.img", "--validate", "--cat", "A"]));
    }

    private static void ParsesRscOptions()
    {
        var options = RscOptions.Parse([
            "--disk", "work.img",
            "--module", "Kernel.rsc",
            "--graph",
            "--source-map",
            "--root", "src",
            "--disassemble",
            "--command", "Start",
            "--count", "64"]);

        TestAssertions.Equal("work.img", options.Disk);
        TestAssertions.Equal("Kernel.rsc", options.ModuleName);
        TestAssertions.True(options.Graph);
        TestAssertions.True(options.SourceMap);
        TestAssertions.Equal("src", options.SourceRoot);
        TestAssertions.True(options.Disassemble);
        TestAssertions.Equal("Start", options.CommandName);
        TestAssertions.Equal(64, options.DisassemblyCount);
        TestAssertions.False(options.Help);
    }

    private static void HandlesRscHelp()
    {
        var options = RscOptions.Parse(["--help", "--unknown"]);

        TestAssertions.True(options.Help);
        TestAssertions.True(RscOptions.Parse(["-h", "--unknown"]).Help);
    }

    private static void RejectsInvalidRscOptions()
    {
        TestAssertions.Throws<ArgumentNullException>(() => RscOptions.Parse(null!));
        TestAssertions.Throws<ArgumentException>(() => RscOptions.Parse([]));
        TestAssertions.Throws<ArgumentException>(() => RscOptions.Parse(["--disk", "work.img", "--count", "0"]));
        TestAssertions.Throws<ArgumentException>(() => RscOptions.Parse(["--disk"]));
        TestAssertions.Throws<ArgumentException>(() => RscOptions.Parse(["--unknown"]));
    }
}
