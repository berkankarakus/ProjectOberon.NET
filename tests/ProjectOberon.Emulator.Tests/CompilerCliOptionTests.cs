internal static class CompilerCliOptionTests
{
    public static void Run()
    {
        ParsesOutputOptions();
        RejectsMissingOutputValue();
        RejectsUnknownOption();
        RejectsSharedOutputPath();
        RejectsMultipleSourcePaths();
        ParsesImportedSymbols();
        RejectsDuplicateImports();
        RejectsInvalidImport();
        RejectsInvalidImportName();
        RejectsImportAliasMismatch();
        LoadsMatchingImport();
        RejectsBlankImportPath();
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
        TestAssertions.Throws<ArgumentNullException>(() => CompilerOptions.Parse(null!));
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

    private static void ParsesImportedSymbols()
    {
        var options = CompilerOptions.Parse(["Demo.Mod", "--import", "Files=Files.smb"]);

        TestAssertions.Equal(1, options.Imports.Count);
        TestAssertions.Equal("Files", options.Imports[0].Name);
        TestAssertions.Equal("Files.smb", options.Imports[0].Path);
    }

    private static void RejectsDuplicateImports()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse([
            "Demo.Mod", "--import", "Files=one.smb", "--import", "Files=two.smb"]));
    }

    private static void RejectsInvalidImport()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse(["Demo.Mod", "--import", "Files.smb"]));
    }

    private static void RejectsInvalidImportName()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerOptions.Parse([
            "Demo.Mod", "--import", "Files.Mod=Files.smb"]));
    }

    private static void RejectsImportAliasMismatch()
    {
        var data = OberonCompilerSymbolFileWriter.Write("Files", []);
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-{Guid.NewGuid():N}.smb");
        File.WriteAllBytes(path, data);
        try
        {
            TestAssertions.Throws<InvalidDataException>(() => CompilerImportLoader.Load([
                new CompilerImport("Other", path)]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void LoadsMatchingImport()
    {
        var data = OberonCompilerSymbolFileWriter.Write("Files", []);
        var path = Path.Combine(Path.GetTempPath(), $"project-oberon-{Guid.NewGuid():N}.smb");
        File.WriteAllBytes(path, data);
        try
        {
            var modules = CompilerImportLoader.Load([
                new CompilerImport("Files", path)]);

            TestAssertions.Equal(1, modules.Count);
            TestAssertions.Equal("Files", modules["Files"].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void RejectsBlankImportPath()
    {
        TestAssertions.Throws<ArgumentException>(() => CompilerImportLoader.Load([
            new CompilerImport("Files", " ")]));
    }
}
