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
        TestAssertions.False(loader.Free("Kernel"));
        var memoryLoader = new OberonModuleMemoryLoader(new Memory(), loader);
        TestAssertions.Throws<OberonModuleLoadException>(() => memoryLoader.Load("Kernel"));
        var graph = new OberonModuleLoader(name => name == "A" ? BuildRsc("A", 100, [("Kernel", 0x1234_5678)], "Start") : null);
        graph.RegisterExternal(symbol);
        var module = graph.Load("A");
        TestAssertions.True(module.Imports[0].IsExternal);
        var boundMemory = new Memory(64 * 1024);
        var boundLoader = new OberonModuleMemoryLoader(boundMemory, graph, 0x1000, 0x100);
        boundLoader.BindExternal(new OberonExternalModuleBinding("Kernel", 0x3000, 0x4000, [0x20]));
        var boundImage = boundLoader.Load("A");
        var boundKernel = boundLoader.LoadedImages.Single(candidate => candidate.Module.Name == "Kernel");
        TestAssertions.Equal(0x3000u, boundKernel.DataAddress);
        TestAssertions.Equal(0x4000u, boundKernel.CodeAddress);
        TestAssertions.Equal(0x20u, boundMemory.Read32(boundKernel.EntryAddress));
        TestAssertions.Equal(boundKernel.DescriptorAddress, boundMemory.Read32(boundImage.ImportAddress));
    }
}
