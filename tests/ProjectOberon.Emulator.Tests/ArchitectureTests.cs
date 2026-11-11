using System.Xml.Linq;

internal static class ArchitectureTests
{
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

    public static void Run()
    {
        CoreHasNoProjectReferences();
        CompilerDependsOnlyOnCore();
        AdaptersDependOnlyOnCore();
        ProductionProjectsDoNotDependOnTests();
        AdaptersDoNotDependOnEachOther();
        SolutionContainsExpectedProjectPaths();
    }

    private static void CompilerDependsOnlyOnCore()
    {
        TestAssertions.True(ReadReferences(CompilerProject).SetEquals([CoreProject]));
    }

    private static void CoreHasNoProjectReferences()
    {
        TestAssertions.True(ReadReferences(CoreProject).Count == 0);
    }

    private static void AdaptersDependOnlyOnCore()
    {
        foreach (var adapter in AdapterProjects)
        {
            var references = ReadReferences(adapter);
            var expected = adapter == "src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj"
                ? new[] { CoreProject, CompilerProject }
                : new[] { CoreProject };
            TestAssertions.True(references.SetEquals(expected));
        }
    }

    private static void ProductionProjectsDoNotDependOnTests()
    {
        foreach (var project in AdapterProjects.Append(CoreProject).Append(CompilerProject))
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

    private static void SolutionContainsExpectedProjectPaths()
    {
        var solution = File.ReadAllText(Path.Combine(RepositoryRoot(), "ProjectOberon.NET.slnx"));
        foreach (var project in AdapterProjects.Append(CoreProject).Append(CompilerProject).Append(TestProject))
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
