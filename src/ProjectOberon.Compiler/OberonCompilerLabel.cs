namespace ProjectOberon.Emulator;

public sealed class OberonCompilerLabel
{
    internal int? Position { get; set; }
    internal List<int> Fixups { get; } = [];
}
