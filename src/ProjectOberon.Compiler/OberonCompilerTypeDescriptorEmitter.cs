using System.Buffers.Binary;

namespace ProjectOberon.Compiler;

internal sealed record OberonCompilerTypeDescriptorResult(
    IReadOnlyList<byte> Payload,
    int FixupTypes,
    IReadOnlyList<int> PointerReferences,
    IReadOnlyDictionary<OberonCompilerType, int> Offsets);

internal static class OberonCompilerTypeDescriptorEmitter
{
    public static OberonCompilerTypeDescriptorResult Emit(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var records = CollectRecordTypes(module).ToArray();
        var offsets = new Dictionary<OberonCompilerType, int>();
        var words = new List<int>();
        var fixupTypes = 0;
        var hasTypeFixup = false;

        foreach (var record in records)
        {
            offsets.Add(record, checked(words.Count * sizeof(int)));
            EmitRecordDescriptor(record, offsets, words, ref fixupTypes, ref hasTypeFixup);
        }

        var payload = new byte[checked(words.Count * sizeof(int))];
        for (var index = 0; index < words.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(index * sizeof(int), sizeof(int)),
                words[index]);
        }

        var pointerReferences = CollectPointerReferences(module, payload.Length);
        return new OberonCompilerTypeDescriptorResult(
            payload,
            hasTypeFixup ? fixupTypes : 0,
            pointerReferences,
            offsets);
    }

    private static IReadOnlyList<OberonCompilerType> CollectRecordTypes(
        OberonCompilerModuleParseResult module)
    {
        var records = new List<OberonCompilerType>();
        var visited = new HashSet<OberonCompilerType>();

        foreach (var symbol in module.ModuleScope?.Objects ?? [])
        {
            CollectRecordTypes(symbol.Type, records, visited);
        }

        return records;
    }

    private static void CollectRecordTypes(
        OberonCompilerType? type,
        List<OberonCompilerType> records,
        HashSet<OberonCompilerType> visited)
    {
        if (type is null || !visited.Add(type))
        {
            return;
        }

        if (type.Form == OberonCompilerTypeForm.Record)
        {
            if (type.Base is not null)
            {
                CollectRecordTypes(type.Base, records, visited);
            }

            records.Add(type);
            foreach (var field in type.Fields)
            {
                CollectRecordTypes(field.Type, records, visited);
            }

            return;
        }

        if (type.Form is OberonCompilerTypeForm.Pointer or OberonCompilerTypeForm.Array)
        {
            CollectRecordTypes(type.Base, records, visited);
        }
    }

    private static void EmitRecordDescriptor(
        OberonCompilerType record,
        IReadOnlyDictionary<OberonCompilerType, int> offsets,
        List<int> words,
        ref int fixupTypes,
        ref bool hasTypeFixup)
    {
        var descriptorStart = words.Count;
        words.Add(GetAllocationSize(record.Size));

        if (record.ParameterCount > 3)
        {
            throw new NotSupportedException(
                $"RSC type descriptors support at most three record extensions: {record.ParameterCount}");
        }

        EmitBaseDescriptorEntries(
            record,
            offsets,
            words,
            ref fixupTypes,
            ref hasTypeFixup);

        while (words.Count < descriptorStart + 4)
        {
            words.Add(-1);
        }

        EmitPointerOffsets(record, 0, words);
        words.Add(-1);
    }

    private static void EmitBaseDescriptorEntries(
        OberonCompilerType record,
        IReadOnlyDictionary<OberonCompilerType, int> offsets,
        List<int> words,
        ref int fixupTypes,
        ref bool hasTypeFixup)
    {
        if (record.Base is not null)
        {
            EmitBaseDescriptorEntries(
                record.Base,
                offsets,
                words,
                ref fixupTypes,
                ref hasTypeFixup);

            if (!offsets.TryGetValue(record.Base, out var baseOffset))
            {
                throw new InvalidOperationException("record base descriptor was not emitted");
            }

            var currentWord = words.Count;
            var previousDistance = currentWord - fixupTypes;
            if (previousDistance is < 1 or > 0xfff)
            {
                throw new InvalidOperationException("RSC type fixup chain distance is out of range");
            }

            if (baseOffset > 0xfff)
            {
                throw new NotSupportedException("RSC type descriptor offsets exceed the fixup range");
            }

            words.Add(checked((baseOffset << 12) | previousDistance));
            fixupTypes = currentWord;
            hasTypeFixup = true;
        }

    }

    private static void EmitPointerOffsets(
        OberonCompilerType type,
        int offset,
        List<int> words)
    {
        switch (type.Form)
        {
            case OberonCompilerTypeForm.Pointer:
            case OberonCompilerTypeForm.Nil:
                words.Add(offset);
                return;
            case OberonCompilerTypeForm.Record:
                if (type.Base is not null)
                {
                    EmitPointerOffsets(type.Base, offset, words);
                }

                foreach (var field in type.Fields)
                {
                    EmitPointerOffsets(field.Type!, checked(offset + (int)field.Value), words);
                }

                return;
            case OberonCompilerTypeForm.Array:
                if (type.Base is null || type.Length < 0)
                {
                    return;
                }

                for (var index = 0; index < type.Length; index++)
                {
                    EmitPointerOffsets(
                        type.Base,
                        checked(offset + index * type.Base.Size),
                        words);
                }

                return;
            default:
                return;
        }
    }

    private static IReadOnlyList<int> CollectPointerReferences(
        OberonCompilerModuleParseResult module,
        int variableDataOffset)
    {
        var references = new List<int>();
        foreach (var symbol in module.ModuleScope?.Objects ?? [])
        {
            if (symbol.Class != OberonCompilerObjectClass.Variable || symbol.Type is null)
            {
                continue;
            }

            CollectPointerReferences(
                symbol.Type,
                checked(variableDataOffset + (int)symbol.Value),
                references);
        }

        return references;
    }

    private static void CollectPointerReferences(
        OberonCompilerType type,
        int offset,
        List<int> references)
    {
        switch (type.Form)
        {
            case OberonCompilerTypeForm.Pointer:
            case OberonCompilerTypeForm.Nil:
                references.Add(offset);
                return;
            case OberonCompilerTypeForm.Record:
                if (type.Base is not null)
                {
                    CollectPointerReferences(type.Base, offset, references);
                }

                foreach (var field in type.Fields)
                {
                    CollectPointerReferences(
                        field.Type!,
                        checked(offset + (int)field.Value),
                        references);
                }

                return;
            case OberonCompilerTypeForm.Array:
                if (type.Base is null || type.Length < 0)
                {
                    return;
                }

                for (var index = 0; index < type.Length; index++)
                {
                    CollectPointerReferences(
                        type.Base,
                        checked(offset + index * type.Base.Size),
                        references);
                }

                return;
            default:
                return;
        }
    }

    private static int GetAllocationSize(int recordSize)
    {
        if (recordSize <= 24)
        {
            return 32;
        }

        if (recordSize <= 56)
        {
            return 64;
        }

        if (recordSize <= 120)
        {
            return 128;
        }

        return checked((recordSize + 263) / 256 * 256);
    }
}
