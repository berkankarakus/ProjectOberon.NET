namespace ProjectOberon.Emulator;

public sealed partial class OberonModuleLoader
{
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
        var normalized = name;
        if (normalized.EndsWith(".rsc", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".smb", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }
        OberonFileNameRules.Validate(normalized);
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
