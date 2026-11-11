internal static class CompilerCliOptionTests
{
    public static void Run()
    {
        ParsesOutputOptions();
        RejectsMissingOutputValue();
        RejectsUnknownOption();
        RejectsSharedOutputPath();
        RejectsMultipleSourcePaths();
    }

    private static void ParsesOutputOptions()
    {
        var options = CompilerOptions.Parse([
            "Demo.Mod", "--symbols", "Demo.smb", "--rsc", "Demo.rsc", "--no-disassemble"]);

        TestAssertions.Equal("Demo.Mod", options.SourcePath);
        TestAssertions.Equal("Demo.smb", options.SymbolPath);
        TestAssertions.Equal("Demo.rsc", options.RscPath);
        TestAssertions.False(options.Disassemble);
        TestAssertions.False(options.Help);
    }

    private static void RejectsMissingOutputValue()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse(["Demo.Mod", "--rsc"]));
    }

    private static void RejectsUnknownOption()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse(["Demo.Mod", "--unknown"]));
    }

    private static void RejectsSharedOutputPath()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse([
            "Demo.Mod", "--symbols", "out/Demo.bin", "--rsc", "out/./Demo.bin"]));
    }

    private static void RejectsMultipleSourcePaths()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse(["First.Mod", "Second.Mod"]));
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse([
            "--source", "First.Mod", "Second.Mod"]));
    }
}
