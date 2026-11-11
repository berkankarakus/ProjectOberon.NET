internal enum FsCommand
{
    List,
    Cat,
    Put,
    Delete,
    Rename
}

internal sealed record FsOptions(
    string? Disk,
    string Prefix,
    bool Writable,
    FsCommand Command,
    string? Name,
    string? Path,
    string? NewName,
    bool Help)
{
    public static FsOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0
            || args.Contains("--help", StringComparer.Ordinal)
            || args.Contains("-h", StringComparer.Ordinal))
        {
            return new FsOptions(null, string.Empty, false, FsCommand.List, null, null, null, Help: true);
        }

        string? disk = null;
        var prefix = string.Empty;
        var writable = false;
        var command = FsCommand.List;
        string? name = null;
        string? path = null;
        string? newName = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--disk" when index + 1 < args.Length:
                    disk = args[++index];
                    break;
                case "--prefix" when index + 1 < args.Length:
                    prefix = args[++index];
                    break;
                case "--writable":
                    writable = true;
                    break;
                case "--cat" when index + 1 < args.Length:
                    EnsureCommand(command, FsCommand.Cat);
                    command = FsCommand.Cat;
                    name = args[++index];
                    break;
                case "--put" when index + 2 < args.Length:
                    EnsureCommand(command, FsCommand.Put);
                    command = FsCommand.Put;
                    name = args[++index];
                    path = args[++index];
                    break;
                case "--delete" when index + 1 < args.Length:
                    EnsureCommand(command, FsCommand.Delete);
                    command = FsCommand.Delete;
                    name = args[++index];
                    break;
                case "--rename" when index + 2 < args.Length:
                    EnsureCommand(command, FsCommand.Rename);
                    command = FsCommand.Rename;
                    name = args[++index];
                    newName = args[++index];
                    break;
                default:
                    throw new ArgumentException($"unknown or incomplete option: {args[index]}");
            }
        }

        if (disk is null)
        {
            throw new ArgumentException("--disk FILE is required");
        }

        if (command is FsCommand.Put or FsCommand.Delete or FsCommand.Rename)
        {
            if (!writable)
            {
                throw new ArgumentException("mutating operations require --writable");
            }
        }

        return new FsOptions(disk, prefix, writable, command, name, path, newName, Help: false);
    }

    private static void EnsureCommand(FsCommand current, FsCommand requested)
    {
        if (current != FsCommand.List)
        {
            throw new ArgumentException($"cannot combine filesystem commands: {current} and {requested}");
        }
    }
}
