using ProjectOberon.Emulator;

try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Options.PrintUsage();
        return;
    }

    using var machine = new Machine();
    if (options.Rom is not null) machine.Memory.LoadRomFile(options.Rom);
    if (options.Disk is not null) machine.OpenDisk(options.Disk, options.DiskWritable);
    var debugger = new MachineDebugger(machine);
    if (options.Breakpoint is uint breakpoint) debugger.AddBreakpoint(breakpoint);
    var loadedModules = options.BootTrace || options.BootGraph ? AttachBootTrace(machine) : null;
    if (options.Trace is int traceSteps)
    {
        for (ulong index = 0; index < options.Steps; index++)
        {
            if (index < (ulong)traceSteps)
            {
                var raw = machine.Memory.ReadInstruction(machine.Cpu.PcByte);
                Console.WriteLine($"{index} pc={machine.Cpu.PcByte:x6} ir={raw:x8} {Risc5Disassembler.Format(raw)} r14={machine.Cpu.Register(14):x8} r15={machine.Cpu.Register(15):x8}");
            }
            if (!debugger.Step()) break;
        }
    }
    else debugger.Run(options.Steps);

    if (debugger.IsAtBreakpoint) Console.Error.WriteLine($"breakpoint reached at pc=0x{machine.Cpu.PcByte:x6}");
    if (loadedModules is not null)
    {
        machine.Disk!.SectorRead -= loadedModules.Handler;
        if (options.BootGraph)
        {
            PrintBootGraph(machine, loadedModules);
            return;
        }
        var fileSystem = new OberonFileSystem(machine.Disk);
        foreach (var loaded in loadedModules.Modules)
        {
            Console.WriteLine($"[boot step={loaded.Step}] {loaded.Name}");
            var entry = loadedModules.Entries[loaded.Name];
            var module = RscModuleReader.Parse(fileSystem.ReadFile(entry));
            var imports = module.Imports.Count == 0 ? "(none)" : string.Join(", ", module.Imports.Select(import => import.Name));
            Console.WriteLine($"         imports: {imports}");
        }
    }

    var cpu = machine.Cpu;
    Console.WriteLine($"steps={cpu.Steps} pc=0x{cpu.PcByte:x6} r14=0x{cpu.Register(14):x8} r15=0x{cpu.Register(15):x8}");
}

catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or OverflowException)
{
    Console.Error.WriteLine($"project-oberon: {error.Message}");
    Environment.ExitCode = 1;
}

static BootTraceState AttachBootTrace(Machine machine)
{
    var disk = machine.Disk ?? throw new InvalidOperationException("boot trace requires an open disk");
    var fileSystem = new OberonFileSystem(disk);
    var entries = fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    var headers = entries.Values.ToDictionary(entry => OberonFileSystem.PhysicalSector(entry.HeaderAddress), entry => entry.Name);
    var modules = new List<BootModuleEvent>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    void Handler(uint sector)
    {
        if (headers.TryGetValue(sector, out var name) && seen.Add(name)) modules.Add(new BootModuleEvent(name, machine.Cpu.Steps));
    }
    disk.SectorRead += Handler;
    return new BootTraceState(modules, entries, Handler);
}

static void PrintBootGraph(Machine machine, BootTraceState state)
{
    var fileSystem = new OberonFileSystem(machine.Disk!);
    var loadedNames = state.Modules.Select(module => System.IO.Path.GetFileNameWithoutExtension(module.Name)).ToHashSet(StringComparer.Ordinal);
    Console.WriteLine("digraph OberonBoot {");
    Console.WriteLine("  rankdir=LR;");
    foreach (var module in state.Modules)
    {
        var node = System.IO.Path.GetFileNameWithoutExtension(module.Name);
        var label = $"{node}\\nstep={module.Step}";
        Console.WriteLine($"  \"{node}\" [label=\"{label}\"];");
    }
    for (var index = 0; index + 1 < state.Modules.Count; index++)
    {
        var current = System.IO.Path.GetFileNameWithoutExtension(state.Modules[index].Name);
        var next = System.IO.Path.GetFileNameWithoutExtension(state.Modules[index + 1].Name);
        Console.WriteLine($"  \"{current}\" -> \"{next}\" [style=invis, weight=10];");
    }
    foreach (var moduleEvent in state.Modules)
    {
        var module = RscModuleReader.Parse(fileSystem.ReadFile(state.Entries[moduleEvent.Name]));
        foreach (var import in module.Imports)
        {
            var style = loadedNames.Contains(import.Name) ? string.Empty : ", style=dashed";
            Console.WriteLine($"  \"{module.Name}\" -> \"{import.Name}\" [{style.TrimStart(',', ' ')}];");
        }
    }
    Console.WriteLine("}");
}

internal sealed record Options(string? Rom, string? Disk, bool DiskWritable, ulong Steps, bool Help, int? Trace, uint? Breakpoint, bool BootTrace, bool BootGraph)
{
    public static Options Parse(string[] args)
    {
        string? rom = null;
        string? disk = null;
        var diskWritable = true;
        ulong steps = 1000;
        var help = false;
        int? trace = null;
        uint? breakpoint = null;
        var bootTrace = false;
        var bootGraph = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    help = true;
                    break;
                case "--rom":
                    rom = Next(args, ref index, "--rom");
                    break;
                case "--steps":
                    if (!ulong.TryParse(Next(args, ref index, "--steps"), out steps))
                        throw new ArgumentException("--steps must be an unsigned integer");
                    break;
                case "--disk":
                    disk = Next(args, ref index, "--disk");
                    break;
                case "--read-only":
                    diskWritable = false;
                    break;
                case "--trace":
                    if (!int.TryParse(Next(args, ref index, "--trace"), out var traceValue) || traceValue < 0)
                        throw new ArgumentException("--trace must be a non-negative integer");
                    trace = traceValue;
                    break;
                case "--breakpoint":
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
                default:
                    throw new ArgumentException($"unknown or incomplete command-line option: {args[index]}");
            }
        }
        if (!diskWritable && disk is null) throw new ArgumentException("--read-only requires --disk FILE");
        if ((bootTrace || bootGraph) && disk is null) throw new ArgumentException("--boot-trace/--boot-graph requires --disk FILE");
        return new Options(rom, disk, diskWritable, steps, help, trace, breakpoint, bootTrace, bootGraph);
    }

    public static void PrintUsage() => Console.Error.WriteLine("usage: project-oberon [--rom FILE] [--disk FILE] [--read-only] [--steps N] [--trace N] [--breakpoint HEX-PC] [--boot-trace] [--boot-graph]");

    private static string Next(string[] args, ref int index, string option)
    {
        if (++index >= args.Length) throw new ArgumentException($"unknown or incomplete command-line option: {option}");
        return args[index];
    }
}

internal sealed record BootModuleEvent(string Name, ulong Step);
internal sealed record BootTraceState(List<BootModuleEvent> Modules, Dictionary<string, OberonDirectoryEntry> Entries, Action<uint> Handler);
