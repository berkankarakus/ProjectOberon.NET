using ProjectOberon.Symbols;

internal static class CompilerImportLoader
{
    public static IReadOnlyDictionary<string, OberonSymbolModule> Load(
        IReadOnlyList<CompilerImport> imports)
    {
        ArgumentNullException.ThrowIfNull(imports);

        var modules = new Dictionary<string, OberonSymbolModule>(StringComparer.Ordinal);
        foreach (var import in imports)
        {
            ArgumentNullException.ThrowIfNull(import);
            ArgumentException.ThrowIfNullOrWhiteSpace(import.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(import.Path);

            var module = OberonSymbolModuleReader.Parse(File.ReadAllBytes(import.Path));
            if (!string.Equals(import.Name, module.Name, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"import alias '{import.Name}' does not match SMB module '{module.Name}'");
            }

            modules.Add(import.Name, module);
        }

        return modules;
    }
}
