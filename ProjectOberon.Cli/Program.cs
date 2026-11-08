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
        OberonCoreHostBinding.Bind(moduleLoader, runtime,
            new OberonKernel(machine.Memory, machine.Disk), files, machine.HostCalls);
        if (options.Initialize)
        {
            var descriptor = InitializeModule(machine, moduleLoader, options.StartModule);
            Console.WriteLine($"initialized module {options.StartModule} descriptor=0x{descriptor:x8}");
        }

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

static uint InitializeModule(Machine machine, OberonModuleLoader modules, string name)
{
    if (!modules.TryGetExternalSymbol("Modules", out var symbol))
    {
        throw new OberonModuleLoadException(5, "Modules symbol module is not available");
    }

    var load = symbol.Exports.FirstOrDefault(export =>
        export.Name == "Load" && (export.IsProcedure ?? export.Class == 1));
    if (load is null)
    {
        throw new OberonModuleLoadException(5, "Modules.Load export is not available");
    }

    const uint nameAddress = 0x600;
    const uint descriptorAddress = 0x700;
    const uint resultAddress = OberonCoreHostBinding.ModulesDataAddress + 20;
    WriteCString(machine.Memory, nameAddress, name);
    machine.Cpu.SetRegister(0, nameAddress);
    machine.Cpu.SetRegister(1, descriptorAddress);
    machine.Cpu.SetRegister(15, Memory.RomBase);
    machine.Cpu.SetProgramCounter(
        OberonCoreHostBinding.ModulesCodeAddress + checked((uint)load.Value * 4));
    machine.Step();

    var result = unchecked((int)machine.Memory.Read32(resultAddress));
    if (result != 0)
    {
        var importing = ReadCString(machine.Memory, OberonCoreHostBinding.ModulesDataAddress + 24, 32);
        var imported = ReadCString(machine.Memory, OberonCoreHostBinding.ModulesDataAddress + 56, 32);
        var detail = imported.Length == 0 ? importing : $"{importing} imports {imported}";
        var reason = result == 7 ? "initializer exceeded the step budget" : $"error code {result}";
        throw new OberonModuleLoadException(result,
            $"Modules.Load failed for {name}: {detail}; {reason}");
    }

    return machine.Memory.Read32(descriptorAddress);
}

static void WriteCString(Memory memory, uint address, string value)
{
    var bytes = System.Text.Encoding.ASCII.GetBytes(value);
    if (bytes.Length >= 32)
    {
        throw new ArgumentException("Oberon module name must be shorter than 32 characters", nameof(value));
    }

    for (var index = 0; index < bytes.Length; index++)
    {
        memory.Write8(address + checked((uint)index), bytes[index]);
    }

    memory.Write8(address + checked((uint)bytes.Length), 0);
}

static string ReadCString(Memory memory, uint address, int maxLength)
{
    var bytes = new List<byte>();
    for (var index = 0; index < maxLength; index++)
    {
        var value = memory.Read8(address + checked((uint)index));
        if (value == 0)
        {
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }

        bytes.Add(value);
    }

    return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
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
