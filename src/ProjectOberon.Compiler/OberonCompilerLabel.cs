namespace ProjectOberon.Compiler;

public sealed class OberonCompilerLabel
{
    internal int? Position { get; set; }
    internal List<int> Fixups { get; } = [];
}
