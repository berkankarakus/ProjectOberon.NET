using ProjectOberon.Emulator;

internal static partial class OberonModuleTests
{
    public static void SymbolBinding()
    {
        var symbol = OberonSymbolModuleReader.Parse(BuildSmb("Kernel", 0x1234_5678, "heapOrg", 3));
        var loader = new OberonModuleLoader(_ => null);
        var external = loader.RegisterExternal(symbol);
        TestAssertions.True(external.IsExternal);
        TestAssertions.Equal(0x1234_5678, external.Definition.Key);
        TestAssertions.True(loader.TryGetExternalSymbol("Kernel.SMB", out var normalizedSymbol));
        TestAssertions.Equal(symbol.Name, normalizedSymbol.Name);
        TestAssertions.False(loader.Free("Kernel"));
        var memoryLoader = new OberonModuleMemoryLoader(new Memory(), loader);
        TestAssertions.Throws<OberonModuleLoadException>(() => memoryLoader.Load("Kernel"));
        var graph = new OberonModuleLoader(name => name == "A" ? BuildRsc("A", 100, [("Kernel", 0x1234_5678)], "Start") : null);
        graph.RegisterExternal(symbol);
        var module = graph.Load("A");
        TestAssertions.True(module.Imports[0].IsExternal);
        var boundMemory = new Memory(64 * 1024);
        var boundLoader = new OberonModuleMemoryLoader(boundMemory, graph, 0x1000, 0x100);
        TestAssertions.Throws<ArgumentException>(() => boundLoader.BindExternal(
            new OberonExternalModuleBinding("Kernel", 0x3000, 0x4000, [2, 0, 0, 0])));
        boundLoader.BindExternal(new OberonExternalModuleBinding("Kernel", 0x3000, 0x4000, [0, 0, 0, 0x20]));
        var boundImage = boundLoader.Load("A");
        var boundKernel = boundLoader.LoadedImages.Single(candidate => candidate.Module.Name == "Kernel");
        TestAssertions.Equal(0x3000u, boundKernel.DataAddress);
        TestAssertions.Equal(0x4000u, boundKernel.CodeAddress);
        TestAssertions.Equal(0x20u, boundMemory.Read32(boundKernel.EntryAddress + 12));
        TestAssertions.Equal(boundKernel.DescriptorAddress, boundMemory.Read32(boundImage.ImportAddress));

        var procedures = OberonSymbolBinding.Procedures(new OberonSymbolModule(
            "Demo", 1, 1,
            [
                new OberonSymbolExport("Run", 1, 3) { IsProcedure = true },
                new OberonSymbolExport("Value", 2, 4),
                new OberonSymbolExport("Type", 5, 5)
            ]));
        TestAssertions.Equal(1, procedures.Count);
        TestAssertions.Equal(12u, OberonSymbolBinding.ScaleOffset(3, 4));
        TestAssertions.Throws<InvalidDataException>(() => OberonSymbolBinding.ScaleOffset(-1, 1));
        TestAssertions.Throws<OverflowException>(() => OberonSymbolBinding.ScaleOffset(2, uint.MaxValue));
        TestAssertions.Throws<InvalidDataException>(() => OberonSymbolBinding.EntryOffsets(
            new OberonSymbolModule(
                "Bad",
                1,
                1,
                [
                    new OberonSymbolExport("Run", 1, -1),
                    new OberonSymbolExport("Other", 1, 2)
                ]),
            1));
    }
}
