namespace ProjectOberon.CompilerCli;

internal static class CompilerOutputWriter
{
    public static void Write(string path, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = Path.GetFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (directoryPath is null)
        {
            throw new InvalidOperationException($"output directory is missing: {path}");
        }

        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp"
        );

        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }

        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }
}
