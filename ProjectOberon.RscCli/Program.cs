using ProjectOberon.Emulator;

string? diskPath = null;
var moduleName = "Oberon.rsc";
var graph = false;
var sourceMap = false;
var sourceRoot = ".";
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--disk" when index + 1 < args.Length:
            diskPath = args[++index];
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
        case "--help":
            Console.WriteLine("usage: project-oberon-rsc --disk FILE [--module NAME] [--graph] [--source-map] [--root PATH]");
            return;
        default:
            throw new ArgumentException($"unknown or incomplete option: {args[index]}");
    }
}

if (diskPath is null) throw new ArgumentException("--disk FILE is required");
using var disk = new DiskImage();
disk.Open(diskPath, writable: false);
var fileSystem = new OberonFileSystem(disk);
if (sourceMap)
{
    var sources = OberonSourceLocator.FindModules(sourceRoot);
    var entries = fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    foreach (var sourceEntry in entries.Values.OrderBy(entry => entry.Name, StringComparer.Ordinal))
    {
        var sourceModule = RscModuleReader.Parse(fileSystem.ReadFile(sourceEntry));
        var source = sources.TryGetValue(sourceModule.Name, out var path) ? path : "(source not found)";
        Console.WriteLine($"{sourceModule.Name,-16} {sourceEntry.Name,-20} {source}");
        if (sourceModule.Commands.Count > 0) Console.WriteLine($"  commands: {string.Join(", ", sourceModule.Commands.Select(command => command.Name))}");
    }
    return;
}
if (graph)
{
    var entries = fileSystem.Enumerate()
        .Where(entry => entry.Name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
    var modules = entries.Values.ToDictionary(entry => System.IO.Path.GetFileNameWithoutExtension(entry.Name), entry => RscModuleReader.Parse(fileSystem.ReadFile(entry)), StringComparer.Ordinal);
    Console.WriteLine("digraph OberonModules {");
    Console.WriteLine("  rankdir=LR;");
    foreach (var parsed in modules.Values)
    {
        if (parsed.Imports.Count == 0) Console.WriteLine($"  \"{parsed.Name}\";");
        foreach (var import in parsed.Imports)
        {
            var target = import.Name;
            if (modules.ContainsKey(target)) Console.WriteLine($"  \"{parsed.Name}\" -> \"{target}\";");
            else Console.WriteLine($"  \"{parsed.Name}\" -> \"{import.Name}\" [style=dashed];");
        }
    }
    Console.WriteLine("}");
    return;
}
var entry = fileSystem.Enumerate(moduleName).SingleOrDefault(e => e.Name.Equals(moduleName, StringComparison.Ordinal));
if (entry is null) throw new FileNotFoundException($"module not found in disk image: {moduleName}");

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
