namespace ProjectOberon.Emulator;

public sealed class OberonModuleLoadException : Exception
{
    public OberonModuleLoadException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}

public sealed class LoadedOberonModule
{
    internal LoadedOberonModule(string name, int number, RscModule definition, IReadOnlyList<LoadedOberonModule> imports)
    {
        Name = name;
        Number = number;
        Definition = definition;
        Imports = imports;
    }

    public string Name { get; }
    public int Number { get; }
    public RscModule Definition { get; }
    public IReadOnlyList<LoadedOberonModule> Imports { get; }
    public int ReferenceCount { get; internal set; }

    public RscCommand? FindCommand(string name)
    {
        return Definition.Commands.FirstOrDefault(command => command.Name == name);
    }
}

public sealed class OberonModuleLoader
{
    private readonly Func<string, byte[]?> _readModule;
    private readonly Dictionary<string, LoadedOberonModule> _loaded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loading = new(StringComparer.Ordinal);
    private int _nextNumber = 1;

    public OberonModuleLoader(OberonFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _readModule = name =>
        {
            var file = files.Old($"{name}.rsc");
            return file is null ? null : ReadAll(file);
        };
    }

    public OberonModuleLoader(Func<string, byte[]?> readModule)
    {
        _readModule = readModule ?? throw new ArgumentNullException(nameof(readModule));
    }

    public IReadOnlyCollection<LoadedOberonModule> LoadedModules => _loaded.Values;

    public LoadedOberonModule Load(string name)
    {
        var normalizedName = NormalizeName(name);
        if (_loaded.TryGetValue(normalizedName, out var loaded))
        {
            return loaded;
        }

        if (!_loading.Add(normalizedName))
        {
            throw new OberonModuleLoadException(5, $"cyclic Oberon module import: {normalizedName}");
        }

        var imports = new List<LoadedOberonModule>();
        var newlyLoadedImports = new List<LoadedOberonModule>();
        try
        {
            var data = _readModule(normalizedName);
            if (data is null)
            {
                throw new OberonModuleLoadException(2, $"Oberon module is not available: {normalizedName}");
            }

            var definition = Parse(normalizedName, data);
            imports.Capacity = definition.Imports.Count;
            foreach (var import in definition.Imports)
            {
                var wasLoaded = _loaded.ContainsKey(import.Name);
                var imported = Load(import.Name);
                if (!wasLoaded)
                {
                    newlyLoadedImports.Add(imported);
                }

                if (imported.Definition.Key != import.Key)
                {
                    throw new OberonModuleLoadException(3,
                        $"Oberon module key conflict: {normalizedName} imports {import.Name}");
                }

                imported.ReferenceCount++;
                imports.Add(imported);
                if (!wasLoaded)
                {
                    newlyLoadedImports.Add(imported);
                }
            }

            loaded = new LoadedOberonModule(normalizedName, _nextNumber++, definition, imports);
            _loaded.Add(normalizedName, loaded);
            return loaded;
        }
        catch
        {
            RollbackFailedLoad(imports, newlyLoadedImports);
            throw;
        }
        finally
        {
            _loading.Remove(normalizedName);
        }
    }

    public bool Free(string name)
    {
        var normalizedName = NormalizeName(name);
        if (!_loaded.TryGetValue(normalizedName, out var module))
        {
            return false;
        }

        if (module.ReferenceCount != 0)
        {
            return false;
        }

        _loaded.Remove(normalizedName);
        foreach (var imported in module.Imports)
        {
            imported.ReferenceCount--;
        }
        return true;
    }

    private static RscModule Parse(string expectedName, byte[] data)
    {
        RscModule definition;
        try
        {
            definition = RscModuleReader.Parse(data);
        }
        catch (InvalidDataException error)
        {
            throw new OberonModuleLoadException(5, $"corrupted Oberon module {expectedName}: {error.Message}");
        }

        if (!string.Equals(definition.Name, expectedName, StringComparison.Ordinal))
        {
            throw new OberonModuleLoadException(5,
                $"Oberon module name mismatch: requested {expectedName}, found {definition.Name}");
        }

        return definition;
    }

    private void RollbackFailedLoad(
        IReadOnlyList<LoadedOberonModule> imports,
        IReadOnlyList<LoadedOberonModule> newlyLoadedImports)
    {
        foreach (var imported in imports)
        {
            imported.ReferenceCount--;
        }

        foreach (var imported in newlyLoadedImports.DistinctBy(module => module.Name))
        {
            if (imported.ReferenceCount == 0)
            {
                Free(imported.Name);
            }
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var normalized = name.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
        OberonFiles.ValidateName(normalized);
        return normalized;
    }

    private static byte[] ReadAll(OberonFile file)
    {
        var data = new byte[file.Length];
        var rider = file.Set();
        var count = rider.ReadBytes(data);
        if (count != data.Length)
        {
            throw new InvalidDataException($"could not read Oberon module {file.Name}");
        }
        return data;
    }
}
