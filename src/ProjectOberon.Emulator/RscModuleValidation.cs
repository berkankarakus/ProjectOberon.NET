namespace ProjectOberon.Emulator;

internal static class RscModuleValidation
{
    public static void ValidateUniqueNames<T>(
        IEnumerable<T> items,
        Func<T, string> nameSelector,
        string collectionName)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nameSelector);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var name = nameSelector(item);
            if (!names.Add(name))
            {
                throw new InvalidDataException($"duplicate RSC {collectionName} name: {name}");
            }
        }
    }

    public static void ValidateCodeLayout(
        int codeWords,
        IReadOnlyList<RscCommand> commands,
        int bodyOffset)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var codeBytes = checked(codeWords * sizeof(uint));
        if (bodyOffset < 0 || bodyOffset > codeBytes || bodyOffset % sizeof(uint) != 0)
        {
            throw new InvalidDataException($"invalid RSC body offset: {bodyOffset}");
        }

        foreach (var command in commands)
        {
            if (command.CodeOffset < 0 || command.CodeOffset >= codeBytes
                || command.CodeOffset % sizeof(uint) != 0)
            {
                throw new InvalidDataException(
                    $"invalid RSC command offset: {command.Name}={command.CodeOffset}");
            }
        }
    }

    public static void ValidateEntryOffsets(int codeWords, IReadOnlyList<int> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var codeBytes = checked(codeWords * sizeof(uint));
        foreach (var entry in entries)
        {
            if (entry < 0 || entry > codeBytes || entry % sizeof(uint) != 0)
            {
                throw new InvalidDataException($"invalid RSC entry offset: {entry}");
            }
        }
    }

    public static void ValidateFixupHeads(
        int codeWords,
        int typeDescriptorBytes,
        int fixupProgram,
        int fixupData,
        int fixupTypes)
    {
        ValidateWordOffset(codeWords, fixupProgram, "program");
        ValidateWordOffset(codeWords, fixupData, "data");

        if (fixupTypes < 0 || (fixupTypes != 0 && fixupTypes * sizeof(uint) >= typeDescriptorBytes))
        {
            throw new InvalidDataException($"invalid RSC types fixup offset: {fixupTypes}");
        }
    }

    public static void ValidatePointerReferences(
        int typeDescriptorBytes,
        int variableBytes,
        int stringBytes,
        IReadOnlyList<int> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        var dataBytes = checked(typeDescriptorBytes + variableBytes + stringBytes);
        foreach (var reference in references)
        {
            if (reference < 0 || reference % sizeof(uint) != 0
                || reference > dataBytes - sizeof(uint))
            {
                throw new InvalidDataException($"invalid RSC pointer reference: {reference}");
            }
        }
    }

    private static void ValidateWordOffset(int wordCount, int offset, string name)
    {
        if (offset < 0 || (offset != 0 && offset >= wordCount))
        {
            throw new InvalidDataException($"invalid RSC {name} fixup offset: {offset}");
        }
    }
}
