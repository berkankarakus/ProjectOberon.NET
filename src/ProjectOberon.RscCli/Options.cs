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
        if (args.Contains("--help", StringComparer.Ordinal)
            || args.Contains("-h", StringComparer.Ordinal))
        {
            return new RscOptions(null, "Oberon.rsc", false, false, ".", false, null, 32, Help: true);
        }

        string? disk = null;
        var moduleName = "Oberon.rsc";
        var graph = false;
        var sourceMap = false;
        var sourceRoot = ".";
        var disassemble = false;
        string? commandName = null;
        var disassemblyCount = 32;
        var help = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--disk" when index + 1 < args.Length:
                    disk = args[++index];
                    break;
                case "--module" when index + 1 < args.Length:
                    moduleName = args[++index];
                    break;
                case "--graph":
                    graph = true;
                    break;
                case "--source-map":
                    sourceMap = true;
                    break;
                case "--root" when index + 1 < args.Length:
                    sourceRoot = args[++index];
                    break;
                case "--disassemble":
                    disassemble = true;
                    break;
                case "--command" when index + 1 < args.Length:
                    commandName = args[++index];
                    break;
                case "--count" when index + 1 < args.Length:
                    if (!int.TryParse(args[++index], out disassemblyCount) || disassemblyCount < 1)
                        throw new ArgumentException("--count must be a positive integer");
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
