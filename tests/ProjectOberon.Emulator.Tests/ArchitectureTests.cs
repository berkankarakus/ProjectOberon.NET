using System.Xml.Linq;

internal static class ArchitectureTests
{
    private const string ContractsProject = "src/ProjectOberon.Contracts/ProjectOberon.Contracts.csproj";
    private const string RscProject = "src/ProjectOberon.Rsc/ProjectOberon.Rsc.csproj";
    private const string SymbolsProject = "src/ProjectOberon.Symbols/ProjectOberon.Symbols.csproj";
    private const string CoreProject = "src/ProjectOberon.Emulator/ProjectOberon.Emulator.csproj";
    private const string CompilerProject = "src/ProjectOberon.Compiler/ProjectOberon.Compiler.csproj";
    private const string TestProject = "tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj";

    private static readonly string[] AdapterProjects =
    [
        "src/ProjectOberon.Cli/ProjectOberon.Cli.csproj",
        "src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj",
        "src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj",
        "src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj",
        "src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj"
    ];

    private static readonly string[] TestReferences =
    [
        CompilerProject,
        CoreProject,
        RscProject,
        SymbolsProject,
        "src/ProjectOberon.Cli/ProjectOberon.Cli.csproj",
        "src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj",
        "src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj",
        "src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj",
        "src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj"
    ];

    public static void Run()
    {
        ContractsHaveNoProjectReferences();
        SymbolsHaveNoProjectReferences();
        RscDependsOnlyOnContracts();
        CoreDependsOnContractsRscAndSymbols();
        CompilerDependsOnContractsAndSymbols();
        AdaptersDependOnlyOnCore();
        ProductionProjectsDoNotDependOnTests();
        AdaptersDoNotDependOnEachOther();
        TestProjectHasExpectedReferences();
        SolutionContainsExpectedProjectPaths();
    }

    private static void CompilerDependsOnContractsAndSymbols()
    {
        TestAssertions.True(ReadReferences(CompilerProject).SetEquals([ContractsProject, SymbolsProject]));
    }

    private static void ContractsHaveNoProjectReferences()
    {
        TestAssertions.True(ReadReferences(ContractsProject).Count == 0);
    }

    private static void CoreDependsOnContractsRscAndSymbols()
    {
        TestAssertions.True(ReadReferences(CoreProject).SetEquals([ContractsProject, RscProject, SymbolsProject]));
    }

    private static void RscDependsOnlyOnContracts()
    {
        TestAssertions.True(ReadReferences(RscProject).SetEquals([ContractsProject]));
    }

    private static void SymbolsHaveNoProjectReferences()
    {
        TestAssertions.True(ReadReferences(SymbolsProject).Count == 0);
    }

    private static void AdaptersDependOnlyOnCore()
    {
        foreach (var adapter in AdapterProjects)
        {
            var references = ReadReferences(adapter);
            var expected = adapter == "src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj"
                ? new[] { CoreProject, CompilerProject, RscProject, SymbolsProject }
                : adapter == "src/ProjectOberon.Cli/ProjectOberon.Cli.csproj"
                    ? new[] { CoreProject, RscProject }
                : adapter == "src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj"
                    ? new[] { CoreProject, RscProject }
                : new[] { CoreProject };
            TestAssertions.True(references.SetEquals(expected));
        }
    }

    private static void ProductionProjectsDoNotDependOnTests()
    {
        foreach (var project in AdapterProjects.Append(CoreProject).Append(CompilerProject).Append(ContractsProject).Append(RscProject).Append(SymbolsProject))
        {
            TestAssertions.False(ReadReferences(project).Contains(TestProject));
        }
    }

    private static void AdaptersDoNotDependOnEachOther()
    {
        foreach (var adapter in AdapterProjects)
        {
            var references = ReadReferences(adapter);
            TestAssertions.True(!references.Intersect(AdapterProjects).Any());
        }
    }

    private static void TestProjectHasExpectedReferences()
    {
        TestAssertions.True(ReadReferences(TestProject).SetEquals(TestReferences));
    }

    private static void SolutionContainsExpectedProjectPaths()
    {
        var solution = File.ReadAllText(Path.Combine(RepositoryRoot(), "ProjectOberon.NET.slnx"));
        foreach (var project in AdapterProjects.Append(CoreProject).Append(CompilerProject).Append(ContractsProject).Append(RscProject).Append(SymbolsProject).Append(TestProject))
        {
            TestAssertions.True(solution.Contains($"Path=\"{project}\"", StringComparison.Ordinal));
        }
    }

    private static HashSet<string> ReadReferences(string project)
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, project.Replace('/', Path.DirectorySeparatorChar));
        var document = XDocument.Load(path);
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var include in document.Descendants("ProjectReference")
                     .Select(element => (string?)element.Attribute("Include"))
                     .Where(include => include is not null))
        {
            var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, include!));
            var relativePath = Path.GetRelativePath(root, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            references.Add(relativePath);
        }

        return references;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ProjectOberon.NET.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("could not locate ProjectOberon.NET.slnx");
    }
}
