using ProjectOberon.Emulator;

Machine? machine = null;
try
{
    var options = Options.Parse(args);
    if (options.Help)
    {
        Options.PrintUsage();
        return;
    }

    machine = new Machine();
    if (options.Rom is not null) machine.Memory.LoadRomFile(options.Rom);
    if (options.Disk is not null) machine.OpenDisk(options.Disk, options.DiskWritable);
    if (options.StartModule is not null)
    {
        var fileSystem = new OberonFileSystem(machine.Disk!);
        var files = new OberonFiles(fileSystem);
        var moduleLoader = new OberonModuleLoader(files, ["Kernel", "Files", "Modules"]);
        var runtime = new OberonModuleRuntime(machine.Memory, machine.Cpu, moduleLoader);
        var image = options.Command is null
            ? runtime.Start(options.StartModule)
            : runtime.StartCommand(options.StartModule, options.Command);
        Console.WriteLine($"started module {image.Module.Name}"
            + (options.Command is null ? string.Empty : $" command {options.Command}")
            + $" at pc=0x{machine.Cpu.PcByte:x6}");
    }

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

catch (Exception error)
{
    Console.Error.WriteLine(OberonRuntimeDiagnostics.Format("project-oberon", machine, error));
    Environment.ExitCode = 1;
}
finally
{
    machine?.Dispose();
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

internal sealed record BootModuleEvent(string Name, ulong Step);
internal sealed record BootTraceState(List<BootModuleEvent> Modules, Dictionary<string, OberonDirectoryEntry> Entries, Action<uint> Handler);
