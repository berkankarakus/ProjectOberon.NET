namespace ProjectOberon.Emulator;

public static class OberonSourceLocator
{
    public static IReadOnlyDictionary<string, string> FindModules(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return result;
        foreach (var path in Directory.EnumerateFiles(root, "*.Mod.txt", SearchOption.AllDirectories))
        {
            var fileName = System.IO.Path.GetFileName(path);
            var moduleName = fileName[..^".Mod.txt".Length];
            result[moduleName] = System.IO.Path.GetRelativePath(root, path);
        }
        return result;
    }
}
