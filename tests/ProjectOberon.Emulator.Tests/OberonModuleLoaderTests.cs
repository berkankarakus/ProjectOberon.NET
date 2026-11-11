using ProjectOberon.Emulator;

internal static partial class OberonModuleTests
{
    public static void LoaderGraph()
    {
        var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start"),
            ["B.rsc"] = BuildRsc("B", 200, [], "Run")
        };
        var loader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
        var module = loader.Load("A.rsc");
        TestAssertions.Equal("A", module.Name);
        TestAssertions.Equal(2, module.Number);
        TestAssertions.Equal("B", module.Imports[0].Name);
        TestAssertions.Equal("Start", module.FindCommand("Start")!.Name);
        TestAssertions.Equal(2, loader.LoadedModules.Count);
        TestAssertions.True(ReferenceEquals(module, loader.Load("A")));
        TestAssertions.True(ReferenceEquals(module, loader.Load("A.RSC")));
        TestAssertions.False(loader.Free("B"));
        TestAssertions.True(loader.Free("A"));
        TestAssertions.True(loader.Free("B"));
        var conflict = new OberonModuleLoader(name => name == "A" ? BuildRsc("A", 100, [("B", 201)], "Start") : modules["B.rsc"]);
        TestAssertions.Throws<OberonModuleLoadException>(() => conflict.Load("A"));
        TestAssertions.Equal(0, conflict.LoadedModules.Count);
        var cycle = new OberonModuleLoader(name => name == "A" ? BuildRsc("A", 100, [("B", 200)], "Start") : BuildRsc("B", 200, [("A", 100)], "Run"));
        TestAssertions.Throws<OberonModuleLoadException>(() => cycle.Load("A"));
        TestAssertions.Equal(0, cycle.LoadedModules.Count);

        var missing = new OberonModuleLoader(_ => null);
        TestAssertions.Throws<OberonModuleLoadException>(() => missing.Load("Missing"));
        TestAssertions.Equal(0, missing.LoadedModules.Count);
        TestAssertions.Throws<ArgumentException>(() => missing.Load("A/B"));
    }
}
