namespace ProjectOberon.Emulator;

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

public static class RscModuleReader
{
    private const byte VersionKey = 0x01;

    public static RscModule Parse(ReadOnlySpan<byte> data)
    {
        var reader = new RscReader(data);
        var name = reader.ReadString();
        var key = reader.ReadInt32();
        var version = reader.ReadByte();
        var size = reader.ReadInt32();
        if (version != VersionKey)
        {
            throw new InvalidDataException($"unsupported RSC version: 0x{version:x2}");
        }

        var imports = new List<RscImport>();
        while (true)
        {
            var importName = reader.ReadString();
            if (importName.Length == 0)
            {
                break;
            }
            imports.Add(new RscImport(importName, reader.ReadInt32()));
        }

        var typeDescriptorBytes = reader.ReadInt32();
        var typeDescriptorPayload = reader.ReadBytes(typeDescriptorBytes);
        var variableBytes = reader.ReadInt32();
        if (variableBytes < 0)
        {
            throw new InvalidDataException($"negative RSC variable space: {variableBytes}");
        }

        // Variable space is allocated in memory by Modules.Load; it has no
        // corresponding payload in the RSC file.
        var stringBytes = reader.ReadInt32();
        var stringPayload = reader.ReadBytes(stringBytes);
        var codeWords = reader.ReadInt32();
        if (codeWords < 0)
        {
            throw new InvalidDataException($"invalid code word count: {codeWords}");
        }
        var code = new uint[codeWords];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = unchecked((uint)reader.ReadInt32());
        }

        var commands = new List<RscCommand>();
        while (true)
        {
            var commandName = reader.ReadString();
            if (commandName.Length == 0)
            {
                break;
            }
            commands.Add(new RscCommand(commandName, reader.ReadInt32()));
        }

        var entryCount = reader.ReadInt32();
        var entries = ReadIntArray(reader, entryCount, "entry table");

        var pointerReferences = new List<int>();
        while (true)
        {
            var offset = reader.ReadInt32();
            if (offset < 0)
            {
                break;
            }
            pointerReferences.Add(offset);
        }

        var fixupProgram = reader.ReadInt32();
        var fixupData = reader.ReadInt32();
        var fixupTypes = reader.ReadInt32();
        var bodyOffset = reader.ReadInt32();
        var trailer = reader.ReadByte();
        if (trailer != 'O')
        {
            throw new InvalidDataException($"invalid RSC trailer: 0x{trailer:x2}");
        }

        return new RscModule(name, key, version, size, imports, typeDescriptorBytes, variableBytes, stringBytes,
            codeWords, code, commands, entries, pointerReferences, fixupProgram, fixupData, fixupTypes, bodyOffset, trailer)
        {
            TypeDescriptorPayload = typeDescriptorPayload,
            StringPayload = stringPayload
        };
    }

    private static IReadOnlyList<int> ReadIntArray(RscReader reader, int count, string name)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"invalid {name} count: {count}");
        }

        var result = new int[count];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = reader.ReadInt32();
        }

        return result;
    }

    private ref struct RscReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public RscReader(ReadOnlySpan<byte> data) { _data = data; _position = 0; }

        public byte ReadByte()
        {
            Ensure(1);
            return _data[_position++];
        }

        public int ReadInt32()
        {
            Ensure(4);
            var value = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(_data[_position..]);
            _position += 4;
            return value;
        }

        public string ReadString()
        {
            var start = _position;
            while (true)
            {
                if (_position >= _data.Length)
                {
                    throw new InvalidDataException("unterminated RSC string");
                }

                if (_data[_position++] == 0)
                {
                    return System.Text.Encoding.ASCII.GetString(_data[start..(_position - 1)]);
                }
            }
        }

        public void Skip(int count)
        {
            if (count < 0)
            {
                throw new InvalidDataException($"negative RSC section length: {count}");
            }

            Ensure(count);
            _position += count;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new InvalidDataException($"negative RSC section length: {count}");
            }

            Ensure(count);
            var result = _data.Slice(_position, count).ToArray();
            _position += count;
            return result;
        }

        public void Align(int alignment)
        {
            var aligned = (_position + alignment - 1) / alignment * alignment;
            Skip(aligned - _position);
        }

        private void Ensure(int count)
        {
            if (count < 0 || count > _data.Length - _position)
            {
                throw new InvalidDataException("truncated RSC module");
            }
        }
    }
}
