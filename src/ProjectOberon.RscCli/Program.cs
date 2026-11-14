using ProjectOberon.Emulator;
using ProjectOberon.Contracts;
using ProjectOberon.Rsc;

var options = RscOptions.Parse(args);
if (options.Help)
{
    PrintUsage();
    return;
}

using var disk = new DiskImage();
disk.Open(options.Disk!, writable: false);
var fileSystem = new OberonFileSystem(disk);

if (options.Disassemble)
{
    Disassemble(fileSystem, options);
    return;
}

if (options.SourceMap)
{
    PrintSourceMap(fileSystem, options.SourceRoot);
    return;
}

if (options.Graph)
{
    PrintModuleGraph(fileSystem);
    return;
}

PrintModule(fileSystem, options);

static void PrintUsage()
{
    Console.WriteLine("usage: project-oberon-rsc --disk FILE [--module NAME] [--graph] [--source-map] [--root PATH] [--disassemble] [--command NAME] [--count N]");
}

static void Disassemble(OberonFileSystem fileSystem, RscOptions options)
{
    var entry = FindModuleEntry(fileSystem, options.ModuleName);
    var module = RscModuleReader.Parse(fileSystem.ReadFile(entry));
    var sourcePath = FindSourcePath(module.Name, options.SourceRoot);
    var startOffset = module.BodyOffset;

    if (options.CommandName is not null)
    {
        var command = module.Commands.SingleOrDefault(candidate =>
            candidate.Name.Equals(options.CommandName, StringComparison.Ordinal));
        if (command is null)
        {
            throw new InvalidOperationException($"command not found in {module.Name}: {options.CommandName}");
        }

        startOffset = command.CodeOffset;
        var sourceProcedure = FindSourceProcedure(sourcePath, options.SourceRoot, options.CommandName);
        var source = sourceProcedure is null ? "unknown" : $"{sourcePath}:{sourceProcedure.Line}";
        Console.WriteLine($"command: {options.CommandName}  code offset={startOffset}  source={source}");
    }
    else
    {
        Console.WriteLine($"module: {module.Name}  code words={module.Code.Count}  body offset={startOffset}");
    }

    var startWord = Math.Max(0, startOffset / 4);
    var endWord = Math.Min(module.Code.Count, startWord + options.DisassemblyCount);
    for (var word = startWord; word < endWord; word++)
    {
        var raw = module.Code[word];
        Console.WriteLine($"  {word * 4,6}: {raw:x8}  {Risc5Disassembler.Format(raw)}");
    }
}

static string? FindSourcePath(string moduleName, string sourceRoot)
{
    return OberonSourceLocator.FindModules(sourceRoot).TryGetValue(moduleName, out var path) ? path : null;
}

static OberonProcedure? FindSourceProcedure(string? sourcePath, string sourceRoot, string commandName)
{
    if (sourcePath is null)
    {
        return null;
    }

    return OberonSourceAnalysis.FindProcedures(Path.Combine(sourceRoot, sourcePath))
        .FirstOrDefault(procedure => procedure.Name == commandName);
}

static OberonDirectoryEntry FindModuleEntry(OberonFileSystem fileSystem, string moduleName)
{
    var entry = fileSystem.Enumerate(moduleName)
        .SingleOrDefault(candidate => candidate.Name.Equals(moduleName, StringComparison.Ordinal));
    return entry ?? throw new FileNotFoundException($"module not found in disk image: {moduleName}");
}

static void PrintSourceMap(OberonFileSystem fileSystem, string sourceRoot)
{
    var sources = OberonSourceLocator.FindModules(sourceRoot);
    var entries = FindRscEntries(fileSystem);
    foreach (var sourceEntry in entries.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal))
    {
        var module = RscModuleReader.Parse(fileSystem.ReadFile(sourceEntry));
        var source = sources.TryGetValue(module.Name, out var path) ? path : "(source not found)";
        Console.WriteLine($"{module.Name,-16} {sourceEntry.Name,-20} {source}");
        PrintSourceMapCommands(module, path, sourceRoot);
    }
}

static void PrintSourceMapCommands(RscModule module, string? sourcePath, string sourceRoot)
{
    if (module.Commands.Count == 0)
    {
        return;
    }

    Console.WriteLine($"  commands: {string.Join(", ", module.Commands.Select(command => command.Name))}");
    if (sourcePath is null)
    {
        return;
    }

    var procedures = OberonSourceAnalysis.FindProcedures(Path.Combine(sourceRoot, sourcePath));
    foreach (var command in module.Commands)
    {
        var sourceProcedure = procedures.FirstOrDefault(procedure => procedure.Name == command.Name);
        var location = sourceProcedure is null ? "source procedure not found" : $"line {sourceProcedure.Line}";
        Console.WriteLine($"    {command.Name,-24} code offset={command.CodeOffset,-5} {location}");
    }

    var exported = procedures.Where(procedure => procedure.Exported)
        .Select(procedure => procedure.Name)
        .ToHashSet(StringComparer.Ordinal);
    var uncompiled = exported.Except(module.Commands.Select(command => command.Name), StringComparer.Ordinal).ToArray();
    if (uncompiled.Length > 0)
    {
        Console.WriteLine($"  exported procedures not in RSC command table: {string.Join(", ", uncompiled)}");
    }
}

static Dictionary<string, OberonDirectoryEntry> FindRscEntries(OberonFileSystem fileSystem)
{
    return fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
}

static void PrintModuleGraph(OberonFileSystem fileSystem)
{
    var entries = FindRscEntries(fileSystem);
    var modules = entries.Values.ToDictionary(
        entry => Path.GetFileNameWithoutExtension(entry.Name),
        entry => RscModuleReader.Parse(fileSystem.ReadFile(entry)),
        StringComparer.Ordinal);

    Console.WriteLine("digraph OberonModules {");
    Console.WriteLine("  rankdir=LR;");
    foreach (var module in modules.Values)
    {
        if (module.Imports.Count == 0)
        {
            Console.WriteLine($"  \"{module.Name}\";");
        }

        foreach (var import in module.Imports)
        {
            var style = modules.ContainsKey(import.Name) ? string.Empty : " [style=dashed]";
            Console.WriteLine($"  \"{module.Name}\" -> \"{import.Name}\"{style};");
        }
    }

    Console.WriteLine("}");
}

static void PrintModule(OberonFileSystem fileSystem, RscOptions options)
{
    var entry = FindModuleEntry(fileSystem, options.ModuleName);
    var module = RscModuleReader.Parse(fileSystem.ReadFile(entry));
    Console.WriteLine($"module: {module.Name}");
    Console.WriteLine($"key: 0x{module.Key:x8}  version: 0x{module.Version:x2}  size: {module.Size}");
    Console.WriteLine($"types: {module.TypeDescriptorBytes}  variables: {module.VariableBytes}  strings: {module.StringBytes}  code words: {module.CodeWords}");
    Console.WriteLine($"fixups: program={module.FixupProgram} data={module.FixupData} types={module.FixupTypes} body={module.BodyOffset}");
    Console.WriteLine("imports:");
    foreach (var import in module.Imports)
    {
        Console.WriteLine($"  {import.Name,-16} key=0x{import.Key:x8}");
    }

    Console.WriteLine("commands:");
    foreach (var command in module.Commands)
    {
        Console.WriteLine($"  {command.Name,-24} offset={command.CodeOffset}");
    }

    Console.WriteLine($"entries: {module.Entries.Count}  pointer references: {module.PointerReferences.Count}");
}
