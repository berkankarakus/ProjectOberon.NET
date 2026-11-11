using ProjectOberon.Emulator;
using ProjectOberon.Rsc;

var options = RscOptions.Parse(args);
if (options.Help)
{
    Console.WriteLine("usage: project-oberon-rsc --disk FILE [--module NAME] [--graph] [--source-map] [--root PATH] [--disassemble] [--command NAME] [--count N]");
    return;
}

using var disk = new DiskImage();
disk.Open(options.Disk!, writable: false);
var fileSystem = new OberonFileSystem(disk);
if (options.Disassemble)
{
    var disassemblyEntry = fileSystem.Enumerate(options.ModuleName).SingleOrDefault(candidate => candidate.Name.Equals(options.ModuleName, StringComparison.Ordinal));
    if (disassemblyEntry is null)
    {
        throw new FileNotFoundException($"module not found in disk image: {options.ModuleName}");
    }
    var disassemblyModule = RscModuleReader.Parse(fileSystem.ReadFile(disassemblyEntry));
    var sourcePath = OberonSourceLocator.FindModules(options.SourceRoot).TryGetValue(disassemblyModule.Name, out var located) ? located : null;
    var startOffset = disassemblyModule.BodyOffset;
    if (options.CommandName is not null)
    {
        var command = disassemblyModule.Commands.SingleOrDefault(candidate => candidate.Name.Equals(options.CommandName, StringComparison.Ordinal));
        if (command is null)
        {
            throw new InvalidOperationException($"command not found in {disassemblyModule.Name}: {options.CommandName}");
        }
        startOffset = command.CodeOffset;
        var sourceProcedure = sourcePath is null ? null : OberonSourceAnalysis.FindProcedures(System.IO.Path.Combine(options.SourceRoot, sourcePath)).FirstOrDefault(procedure => procedure.Name == options.CommandName);
        Console.WriteLine($"command: {options.CommandName}  code offset={startOffset}  source={(sourceProcedure is null ? "unknown" : $"{sourcePath}:{sourceProcedure.Line}")}");
    }
    else Console.WriteLine($"module: {disassemblyModule.Name}  code words={disassemblyModule.Code.Count}  body offset={startOffset}");
    var startWord = Math.Max(0, startOffset / 4);
    var endWord = Math.Min(disassemblyModule.Code.Count, startWord + options.DisassemblyCount);
    for (var word = startWord; word < endWord; word++)
    {
        var raw = disassemblyModule.Code[word];
        Console.WriteLine($"  {word * 4,6}: {raw:x8}  {Risc5Disassembler.Format(raw)}");
    }
    return;
}
if (options.SourceMap)
{
    var sources = OberonSourceLocator.FindModules(options.SourceRoot);
    var entries = fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    foreach (var sourceEntry in entries.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal))
    {
        var sourceModule = RscModuleReader.Parse(fileSystem.ReadFile(sourceEntry));
        var source = sources.TryGetValue(sourceModule.Name, out var path) ? path : "(source not found)";
        Console.WriteLine($"{sourceModule.Name,-16} {sourceEntry.Name,-20} {source}");
        if (sourceModule.Commands.Count > 0)
        {
            Console.WriteLine($"  commands: {string.Join(", ", sourceModule.Commands.Select(command => command.Name))}");
            if (path is not null)
            {
                var procedures = OberonSourceAnalysis.FindProcedures(System.IO.Path.Combine(options.SourceRoot, path));
                foreach (var command in sourceModule.Commands)
                {
                    var sourceProcedure = procedures.FirstOrDefault(procedure => procedure.Name == command.Name);
                    var location = sourceProcedure is null ? "source procedure not found" : $"line {sourceProcedure.Line}";
                    Console.WriteLine($"    {command.Name,-24} code offset={command.CodeOffset,-5} {location}");
                }
                var exported = procedures.Where(procedure => procedure.Exported).Select(procedure => procedure.Name).ToHashSet(StringComparer.Ordinal);
                var uncompiled = exported.Except(sourceModule.Commands.Select(command => command.Name), StringComparer.Ordinal).ToArray();
                if (uncompiled.Length > 0)
                {
                    Console.WriteLine($"  exported procedures not in RSC command table: {string.Join(", ", uncompiled)}");
                }
            }
        }
    }
    return;
}
if (options.Graph)
{
    var entries = fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    var modules = entries.Values.ToDictionary(entry => System.IO.Path.GetFileNameWithoutExtension(entry.Name), entry => RscModuleReader.Parse(fileSystem.ReadFile(entry)), StringComparer.Ordinal);
    Console.WriteLine("digraph OberonModules {");
    Console.WriteLine("  rankdir=LR;");
    foreach (var parsed in modules.Values)
    {
        if (parsed.Imports.Count == 0)
        {
            Console.WriteLine($"  \"{parsed.Name}\";");
        }
        foreach (var import in parsed.Imports)
        {
            var target = import.Name;
            if (modules.ContainsKey(target))
            {
                Console.WriteLine($"  \"{parsed.Name}\" -> \"{target}\";");
            }
            else Console.WriteLine($"  \"{parsed.Name}\" -> \"{import.Name}\" [style=dashed];");
        }
    }
    Console.WriteLine("}");
    return;
}
var entry = fileSystem.Enumerate(options.ModuleName).SingleOrDefault(e => e.Name.Equals(options.ModuleName, StringComparison.Ordinal));
if (entry is null)
{
    throw new FileNotFoundException($"module not found in disk image: {options.ModuleName}");
}

var module = RscModuleReader.Parse(fileSystem.ReadFile(entry));
Console.WriteLine($"module: {module.Name}");
Console.WriteLine($"key: 0x{module.Key:x8}  version: 0x{module.Version:x2}  size: {module.Size}");
Console.WriteLine($"types: {module.TypeDescriptorBytes}  variables: {module.VariableBytes}  strings: {module.StringBytes}  code words: {module.CodeWords}");
Console.WriteLine($"fixups: program={module.FixupProgram} data={module.FixupData} types={module.FixupTypes} body={module.BodyOffset}");
Console.WriteLine("imports:");
foreach (var import in module.Imports) Console.WriteLine($"  {import.Name,-16} key=0x{import.Key:x8}");
Console.WriteLine("commands:");
foreach (var command in module.Commands) Console.WriteLine($"  {command.Name,-24} offset={command.CodeOffset}");
Console.WriteLine($"entries: {module.Entries.Count}  pointer references: {module.PointerReferences.Count}");
