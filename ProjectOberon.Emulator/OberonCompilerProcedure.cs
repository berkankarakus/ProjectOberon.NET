namespace ProjectOberon.Emulator;

public sealed class OberonCompilerProcedureDefinition
{
    public OberonCompilerProcedureDefinition(
        OberonCompilerObject symbol,
        int frameSize,
        OberonCompilerStatementSequence? body,
        IReadOnlyList<OberonCompilerProcedureDefinition> nestedProcedures)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(nestedProcedures);

        Symbol = symbol;
        FrameSize = frameSize;
        Body = body;
        NestedProcedures = nestedProcedures;
    }

    public OberonCompilerObject Symbol { get; }
    public int FrameSize { get; }
    public OberonCompilerStatementSequence? Body { get; }
    public IReadOnlyList<OberonCompilerProcedureDefinition> NestedProcedures { get; }
}
