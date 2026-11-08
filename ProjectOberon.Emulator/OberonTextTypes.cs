namespace ProjectOberon.Emulator;

public enum OberonTextOperation
{
    Replace,
    Insert,
    Delete,
    Unmark
}

public readonly record struct OberonTextStyle(string? Font, int Color, int VerticalOffset)
{
    public static OberonTextStyle Default => new(null, 1, 0);
}
