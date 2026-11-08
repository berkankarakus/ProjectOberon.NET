namespace ProjectOberon.Emulator;

public sealed class OberonFiles
{
    private readonly IOberonFilesBackend _backend;

    public OberonFiles(OberonFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _backend = new ReadOnlyOberonFilesBackend(fileSystem);
    }

    public OberonFiles(OberonKernel kernel, bool initializeDirectory = false)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _backend = new PersistentOberonFilesBackend(kernel, initializeDirectory);
    }

    public OberonFile? Old(string name)
    {
        ValidateName(name);
        return _backend.Old(name);
    }

    public OberonFile New(string name)
    {
        ValidateName(name);
        return _backend.New(name);
    }

    public void Register(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _backend.Register(file);
    }

    public OberonText OpenText(string name)
    {
        ValidateName(name);
        var file = Old(name) ?? throw new FileNotFoundException($"Oberon file not found: {name}");
        return OberonTextCodec.Load(file);
    }

    public void RegisterText(string name, OberonText text, int date = 0)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(text);
        if (_backend is not ReadOnlyOberonFilesBackend && Old(name) is not null)
        {
            throw new IOException($"Oberon file already exists: {name}");
        }

        Register(OberonTextCodec.CreateFile(name, text, date));
    }

    public bool Delete(string name)
    {
        ValidateName(name);
        return _backend.Delete(name);
    }

    public bool Rename(string oldName, string newName)
    {
        ValidateName(oldName);
        ValidateName(newName);
        return _backend.Rename(oldName, newName);
    }

    public void Purge(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _backend.Purge(file);
    }

    public void Close(OberonFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _backend.Close(file);
    }

    public static void ValidateName(string name)
    {
        OberonFileNameRules.Validate(name);
    }
}
