internal sealed record Options(
    string? Rom,
    string? Disk,
    bool DiskWritable,
    ulong Steps,
    bool Help,
    int? Trace,
    int? TraceHistory,
    ulong? ProgressInterval,
    uint? Breakpoint,
    bool BootTrace,
    bool BootGraph,
    string? StartModule,
    bool Initialize,
    bool Boot,
    string? Command)
{
    public static Options Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains("--help", StringComparer.Ordinal)
            || args.Contains("-h", StringComparer.Ordinal))
        {
            return new Options(null, null, true, 1000, true, null, null, null, null,
                false, false, null, false, false, null);
        }

        string? rom = null;
        string? disk = null;
        var diskWritable = true;
        ulong steps = 1000;
        var stepsSpecified = false;
        var help = false;
        int? trace = null;
        int? traceHistory = null;
        ulong? progressInterval = null;
        uint? breakpoint = null;
        var bootTrace = false;
        var bootGraph = false;
        string? startModule = null;
        var initialize = false;
        var boot = false;
        string? command = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "--rom":
                    if (rom is not null)
                    {
                        throw new ArgumentException("ROM file specified more than once");
                    }

                    rom = Next(args, ref index, "--rom");
                    break;
                case "--steps":
                    if (stepsSpecified)
                    {
                        throw new ArgumentException("step count specified more than once");
                    }

                    if (!ulong.TryParse(Next(args, ref index, "--steps"), out steps))
                        throw new ArgumentException("--steps must be an unsigned integer");
                    stepsSpecified = true;
                    break;
                case "--disk":
                    if (disk is not null)
                    {
                        throw new ArgumentException("disk file specified more than once");
                    }

                    disk = Next(args, ref index, "--disk");
                    break;
                case "--read-only":
                    diskWritable = false;
                    break;
                case "--trace":
                    if (trace is not null)
                    {
                        throw new ArgumentException("trace count specified more than once");
                    }

                    if (!int.TryParse(Next(args, ref index, "--trace"), out var traceValue) || traceValue < 0)
                        throw new ArgumentException("--trace must be a non-negative integer");
                    trace = traceValue;
                    break;
                case "--trace-history":
                    if (traceHistory is not null)
                    {
                        throw new ArgumentException("trace history size specified more than once");
                    }

                    if (!int.TryParse(Next(args, ref index, "--trace-history"), out var traceHistoryValue)
                        || traceHistoryValue <= 0)
                    {
                        throw new ArgumentException("--trace-history must be a positive integer");
                    }

                    traceHistory = traceHistoryValue;
                    break;
                case "--progress":
                    if (progressInterval is not null)
                    {
                        throw new ArgumentException("progress interval specified more than once");
                    }

                    if (!ulong.TryParse(Next(args, ref index, "--progress"), out var progressValue)
                        || progressValue == 0)
                    {
                        throw new ArgumentException("--progress must be a positive unsigned integer");
                    }

                    progressInterval = progressValue;
                    break;
                case "--breakpoint":
                    if (breakpoint is not null)
                    {
                        throw new ArgumentException("breakpoint specified more than once");
                    }

                    var breakpointText = Next(args, ref index, "--breakpoint").Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (!uint.TryParse(breakpointText, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var breakpointValue))
                        throw new ArgumentException("--breakpoint must be a hexadecimal PC address");
                    breakpoint = breakpointValue;
                    break;
                case "--boot-trace":
                    bootTrace = true;
                    break;
                case "--boot-graph":
                    bootGraph = true;
                    break;
                case "--start-module":
                    startModule = Next(args, ref index, "--start-module");
                    break;
                case "--initialize":
                    initialize = true;
                    break;
                case "--boot":
                    boot = true;
                    break;
                case "--command":
                    command = Next(args, ref index, "--command");
                    break;
                default:
                    throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }

        if (!diskWritable && disk is null)
            throw new ArgumentException("--read-only requires --disk FILE");
        if ((bootTrace || bootGraph) && disk is null)
            throw new ArgumentException("--boot-trace/--boot-graph requires --disk FILE");
        if (startModule is not null && disk is null)
            throw new ArgumentException("--start-module requires --disk FILE");
        if (initialize && startModule is null)
            throw new ArgumentException("--initialize requires --start-module NAME");
        if (boot && startModule is null)
            throw new ArgumentException("--boot requires --start-module NAME");
        if (initialize && boot)
            throw new ArgumentException("--initialize and --boot cannot be used together");
        if (command is not null && startModule is null)
            throw new ArgumentException("--command requires --start-module NAME");

        return new Options(rom, disk, diskWritable, steps, help, trace, traceHistory, progressInterval, breakpoint, bootTrace, bootGraph,
            startModule, initialize, boot, command);
    }

    public static void PrintUsage() => Console.Error.WriteLine(
        "usage: project-oberon [--rom FILE] [--disk FILE] [--read-only] [--steps N] [--trace N] [--trace-history N] [--progress N] [--breakpoint HEX-PC] [--boot-trace] [--boot-graph] [--start-module NAME [--initialize|--boot] [--command NAME]]");

    private static string Next(string[] args, ref int index, string option)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"unknown or incomplete command-line option: {option}");
        return args[index];
    }
}
