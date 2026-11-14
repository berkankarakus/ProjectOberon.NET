using ProjectOberon.Emulator;

var options = FsOptions.Parse(args);
if (options.Help)
{
    Console.WriteLine("usage: project-oberon-fs --disk FILE [--prefix PREFIX]");
    Console.WriteLine("       project-oberon-fs --disk FILE --cat NAME");
    Console.WriteLine("       project-oberon-fs --disk FILE --put NAME HOST-FILE --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --delete NAME --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --rename OLD NEW --writable");
    Console.WriteLine("       project-oberon-fs --disk FILE --validate");
    return;
}

using var disk = new DiskImage();
disk.Open(options.Disk!, writable: options.Writable);
if (options.Command == FsCommand.List)
{
    var fileSystem = new OberonFileSystem(disk);
    foreach (var entry in fileSystem.Enumerate(options.Prefix))
    {
        var header = fileSystem.ReadHeader(entry.HeaderAddress);
        Console.WriteLine($"{entry.Name,-16} {header.ContentLength,8} bytes  header={entry.HeaderAddress}");
    }
}
else if (options.Command == FsCommand.Cat)
{
    var fileSystem = new OberonFileSystem(disk);
    var entry = fileSystem.Enumerate().SingleOrDefault(item => item.Name == options.Name);
if (entry is null)
{
    throw new FileNotFoundException($"Oberon file not found: {options.Name}");
}
    using var output = Console.OpenStandardOutput();
    output.Write(fileSystem.ReadFile(entry));
}
else if (options.Command == FsCommand.Validate)
{
    var fileSystem = new OberonFileSystem(disk);
    fileSystem.Validate();
    Console.WriteLine("filesystem is valid");
}
else
{
    var kernel = new OberonKernel(new Memory(), disk);
    var files = new OberonFiles(kernel);
    switch (options.Command)
    {
        case FsCommand.Put:
            if (files.Old(options.Name!) is not null)
                throw new IOException($"Oberon file already exists: {options.Name}");
            var file = files.New(options.Name!);
            file.Set().Write(File.ReadAllBytes(options.Path!));
            files.Register(file);
            Console.WriteLine($"created {file.Name} ({file.Length} bytes)");
            break;
        case FsCommand.Delete:
            if (!files.Delete(options.Name!))
                throw new FileNotFoundException($"Oberon file not found: {options.Name}");
            Console.WriteLine($"deleted {options.Name}");
            break;
        case FsCommand.Rename:
            if (files.Old(options.NewName!) is not null)
                throw new IOException($"Oberon file already exists: {options.NewName}");
            if (!files.Rename(options.Name!, options.NewName!))
                throw new FileNotFoundException($"Oberon file not found: {options.Name}");
            Console.WriteLine($"renamed {options.Name} to {options.NewName}");
            break;
    }
}
