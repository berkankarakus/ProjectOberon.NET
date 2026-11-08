namespace ProjectOberon.Emulator;

public sealed record OberonTextLine(int Start, int Length, int Width, string Text);

public readonly record struct OberonCaretLocation(int X, int Y, int Line);
