namespace ProjectOberon.Emulator;

public sealed partial class OberonModuleLoader
{
    private readonly Func<string, byte[]?> _readModule;
    private readonly Dictionary<string, LoadedOberonModule> _loaded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OberonSymbolModule> _externalSymbols = new(StringComparer.Ordinal);
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

    public OberonModuleLoader(OberonFiles files, IEnumerable<string> externalModuleNames)
        : this(files)
    {
        ArgumentNullException.ThrowIfNull(externalModuleNames);
        foreach (var externalModuleName in externalModuleNames)
        {
            var name = NormalizeName(externalModuleName);
            var file = files.Old($"{name}.smb");
            if (file is null)
            {
                continue;
            }

            RegisterExternal(OberonSymbolModuleReader.Parse(ReadAll(file)));
        }
    }

    public OberonModuleLoader(Func<string, byte[]?> readModule)
    {
        _readModule = readModule ?? throw new ArgumentNullException(nameof(readModule));
    }

    public IReadOnlyCollection<LoadedOberonModule> LoadedModules => _loaded.Values;

    public bool TryGetExternalSymbol(string name, out OberonSymbolModule symbol)
    {
        return _externalSymbols.TryGetValue(NormalizeName(name), out symbol!);
    }

    public LoadedOberonModule RegisterExternal(OberonSymbolModule symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var normalizedName = NormalizeName(symbol.Name);
        if (_loaded.TryGetValue(normalizedName, out var existing))
        {
            if (existing.Definition.Key != symbol.Key)
            {
                throw new OberonModuleLoadException(3, $"Oberon module key conflict: {normalizedName}");
            }

            return existing;
        }

        var entryCount = OberonSymbolBinding.EntryOffsets(symbol, 1).Count;
        var entries = new int[entryCount];
        var definition = new RscModule(normalizedName, symbol.Key, symbol.Version, 0, [], 0, 0, 0,
            0, [], [], entries, [], 0, 0, 0, 0, (byte)'O');
        var module = new LoadedOberonModule(normalizedName, _nextNumber++, definition, [], true);
        _loaded.Add(normalizedName, module);
        _externalSymbols[normalizedName] = symbol;
        return module;
    }

    public bool Free(string name)
    {
        var normalizedName = NormalizeName(name);
        if (!_loaded.TryGetValue(normalizedName, out var module))
        {
            return false;
        }

        if (module.IsExternal)
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
}
