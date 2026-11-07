using ProjectOberon.Emulator;

if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
{
    Console.WriteLine("usage: project-oberon-fs --disk FILE [--prefix PREFIX]");
    return;
}

string? diskPath = null;
var prefix = string.Empty;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--disk" when index + 1 < args.Length:
            diskPath = args[++index];
            break;
        case "--prefix" when index + 1 < args.Length:
            prefix = args[++index];
            break;
        default:
            throw new ArgumentException($"unknown or incomplete option: {args[index]}");
    }
}

if (diskPath is null) throw new ArgumentException("--disk FILE is required");
using var disk = new DiskImage();
disk.Open(diskPath, writable: false);
var fileSystem = new OberonFileSystem(disk);
foreach (var entry in fileSystem.Enumerate(prefix))
{
    var header = fileSystem.ReadHeader(entry.HeaderAddress);
    Console.WriteLine($"{entry.Name,-16} {header.ContentLength,8} bytes  header={entry.HeaderAddress}");
}
