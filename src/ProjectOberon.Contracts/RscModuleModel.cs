namespace ProjectOberon.Contracts;

public sealed record RscImport(string Name, int Key);
public sealed record RscCommand(string Name, int CodeOffset);

public sealed record RscModule(
    string Name,
    int Key,
    byte Version,
    int Size,
    IReadOnlyList<RscImport> Imports,
    int TypeDescriptorBytes,
    int VariableBytes,
    int StringBytes,
    int CodeWords,
    IReadOnlyList<uint> Code,
    IReadOnlyList<RscCommand> Commands,
    IReadOnlyList<int> Entries,
    IReadOnlyList<int> PointerReferences,
    int FixupProgram,
    int FixupData,
    int FixupTypes,
    int BodyOffset,
    byte Trailer)
{
    public byte[] TypeDescriptorPayload { get; init; } = [];
    public byte[] StringPayload { get; init; } = [];
}
