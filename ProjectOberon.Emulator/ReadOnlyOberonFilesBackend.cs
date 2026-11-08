namespace ProjectOberon.Emulator;

internal sealed class ReadOnlyOberonFilesBackend : IOberonFilesBackend
{
    private readonly OberonFileSystem _fileSystem;

    public ReadOnlyOberonFilesBackend(OberonFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public OberonFile? Old(string name)
    {
        var entry = _fileSystem.Enumerate().FirstOrDefault(item => item.Name == name);
        if (entry is null)
        {
            return null;
        }

        var date = _fileSystem.ReadHeader(entry.HeaderAddress).Date;
        return new OberonFile(entry.Name, _fileSystem.ReadFile(entry), writable: false, date);
    }

    public OberonFile New(string name)
    {
        return new OberonFile(name, ReadOnlySpan<byte>.Empty, writable: true);
    }

    public void Register(OberonFile file)
    {
        throw PersistentOnly();
    }

    public bool Delete(string name)
    {
        throw PersistentOnly();
    }

    public bool Rename(string oldName, string newName)
    {
        throw PersistentOnly();
    }

    public void Purge(OberonFile file)
    {
        if (file.HeaderAddress != 0)
        {
            throw PersistentOnly();
        }
    }

    public void Close(OberonFile file)
    {
    }

    private static InvalidOperationException PersistentOnly()
    {
        return new InvalidOperationException("persistent file operations require a Kernel-backed Files instance");
    }
}
