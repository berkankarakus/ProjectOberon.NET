internal sealed record RscOptions(
    string? Disk,
    string ModuleName,
    bool Graph,
    bool SourceMap,
    string SourceRoot,
    bool Disassemble,
    string? CommandName,
    int DisassemblyCount,
    bool Help)
{
    public static RscOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains("--help", StringComparer.Ordinal)
            || args.Contains("-h", StringComparer.Ordinal))
        {
            return new RscOptions(null, "Oberon.rsc", false, false, ".", false, null, 32, Help: true);
        }

        string? disk = null;
        var diskSpecified = false;
        var moduleName = "Oberon.rsc";
        var moduleSpecified = false;
        var graph = false;
        var sourceMap = false;
        var sourceRoot = ".";
        var sourceRootSpecified = false;
        var disassemble = false;
        string? commandName = null;
        var commandSpecified = false;
        var disassemblyCount = 32;
        var countSpecified = false;
        var help = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--disk" when index + 1 < args.Length:
                    if (diskSpecified)
                        throw new ArgumentException("disk file specified more than once");
                    disk = args[++index];
                    diskSpecified = true;
                    break;
                case "--module" when index + 1 < args.Length:
                    if (moduleSpecified)
                        throw new ArgumentException("module specified more than once");
                    moduleName = args[++index];
                    moduleSpecified = true;
                    break;
                case "--graph":
                    graph = true;
                    break;
                case "--source-map":
                    sourceMap = true;
                    break;
                case "--root" when index + 1 < args.Length:
                    if (sourceRootSpecified)
                        throw new ArgumentException("source root specified more than once");
                    sourceRoot = args[++index];
                    sourceRootSpecified = true;
                    break;
                case "--disassemble":
                    disassemble = true;
                    break;
                case "--command" when index + 1 < args.Length:
                    if (commandSpecified)
                        throw new ArgumentException("command specified more than once");
                    commandName = args[++index];
                    commandSpecified = true;
                    break;
                case "--count" when index + 1 < args.Length:
                    if (countSpecified)
                        throw new ArgumentException("disassembly count specified more than once");
                    if (!int.TryParse(args[++index], out disassemblyCount) || disassemblyCount < 1)
                        throw new ArgumentException("--count must be a positive integer");
                    countSpecified = true;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
                default:
                    throw new ArgumentException($"unknown or incomplete option: {args[index]}");
            }
        }

        if (help)
        {
            return new RscOptions(null, moduleName, graph, sourceMap, sourceRoot, disassemble, commandName, disassemblyCount, Help: true);
        }
        if (disk is null)
        {
            throw new ArgumentException("--disk FILE is required");
        }
        return new RscOptions(disk, moduleName, graph, sourceMap, sourceRoot, disassemble, commandName, disassemblyCount, Help: false);
    }
}
