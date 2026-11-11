namespace ProjectOberon.Compiler;

public static class OberonCompilerDiagnostics
{
    public static string Format(
        string sourcePath,
        IReadOnlyList<OberonCompilerDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{sourcePath}:{diagnostic.Line}:{diagnostic.Column}: error: {diagnostic.Message}"));
    }
}
