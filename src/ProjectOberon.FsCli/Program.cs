using ProjectOberon.Emulator;

var options = FsOptions.Parse(args);
if (options.Help)
{
    PrintUsage();
    return;
}

using var disk = new DiskImage();
disk.Open(options.Disk!, writable: options.Writable);

switch (options.Command)
{
    case FsCommand.List:
        ListFiles(disk, options.Prefix);
        break;
    case FsCommand.Cat:
        CatFile(disk, options.Name!);
        break;
    case FsCommand.Validate:
        ValidateFileSystem(disk);
        break;
    default:
        ApplyFileCommand(disk, options);
        break;
}

static void PrintUsage()
{
    Console.WriteLine("usage: project-oberon-fs --disk FILE [--prefix PREFIX]");
    Console.WriteLine("       project-oberon-fs --disk FILE --cat NAME");
    Console.WriteLine("       project-oberon-fs --disk FILE --put NAME HOST-FILE --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --delete NAME --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --rename OLD NEW --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --validate");
}

static void ListFiles(DiskImage disk, string? prefix)
{
    var fileSystem = new OberonFileSystem(disk);
    foreach (var entry in fileSystem.Enumerate(prefix ?? string.Empty))
    {
        var header = fileSystem.ReadHeader(entry.HeaderAddress);
        Console.WriteLine($"{entry.Name,-16} {header.ContentLength,8} bytes  header={entry.HeaderAddress}");
    }
}

static void CatFile(DiskImage disk, string name)
{
    var fileSystem = new OberonFileSystem(disk);
    var entry = fileSystem.Enumerate().SingleOrDefault(item => item.Name == name);
    if (entry is null)
    {
        throw new FileNotFoundException($"Oberon file not found: {name}");
    }

    using var output = Console.OpenStandardOutput();
    output.Write(fileSystem.ReadFile(entry));
}

static void ValidateFileSystem(DiskImage disk)
{
    new OberonFileSystem(disk).Validate();
    Console.WriteLine("filesystem is valid");
}

static void ApplyFileCommand(DiskImage disk, FsOptions options)
{
    var kernel = new OberonKernel(new Memory(), disk);
    var files = new OberonFiles(kernel);
    switch (options.Command)
    {
        case FsCommand.Put:
            CreateFile(files, options.Name!, options.Path!);
            break;
        case FsCommand.Delete:
            DeleteFile(files, options.Name!);
            break;
        case FsCommand.Rename:
            RenameFile(files, options.Name!, options.NewName!);
            break;
        default:
            throw new InvalidOperationException($"unsupported filesystem command: {options.Command}");
    }
}

static void CreateFile(OberonFiles files, string name, string hostPath)
{
    if (files.Old(name) is not null)
    {
        throw new IOException($"Oberon file already exists: {name}");
    }

    var file = files.New(name);
    file.Set().Write(File.ReadAllBytes(hostPath));
    files.Register(file);
    Console.WriteLine($"created {file.Name} ({file.Length} bytes)");
}

static void DeleteFile(OberonFiles files, string name)
{
    if (!files.Delete(name))
    {
        throw new FileNotFoundException($"Oberon file not found: {name}");
    }

    Console.WriteLine($"deleted {name}");
}

static void RenameFile(OberonFiles files, string oldName, string newName)
{
    if (files.Old(newName) is not null)
    {
        throw new IOException($"Oberon file already exists: {newName}");
    }

    if (!files.Rename(oldName, newName))
    {
        throw new FileNotFoundException($"Oberon file not found: {oldName}");
    }

    Console.WriteLine($"renamed {oldName} to {newName}");
}
