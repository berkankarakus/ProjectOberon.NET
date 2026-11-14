using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace ProjectOberon.Rsc;

public static class RscModuleWriter
{
    private const byte VersionKey = 0x01;
    private const byte Trailer = (byte)'O';

    public static byte[] Write(RscModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        ValidateModule(module);

        var writer = new BufferWriter();
        writer.WriteString(module.Name);
        writer.WriteInt32(module.Key);
        writer.WriteByte(module.Version);
        writer.WriteInt32(module.Size);

        foreach (var import in module.Imports)
        {
            writer.WriteString(import.Name);
            writer.WriteInt32(import.Key);
        }

        writer.WriteString(string.Empty);
        writer.WriteInt32(module.TypeDescriptorBytes);
        writer.WriteBytes(module.TypeDescriptorPayload);
        writer.WriteInt32(module.VariableBytes);
        writer.WriteInt32(module.StringBytes);
        writer.WriteBytes(module.StringPayload);
        writer.WriteInt32(module.CodeWords);
        foreach (var word in module.Code)
        {
            writer.WriteInt32(unchecked((int)word));
        }

        foreach (var command in module.Commands)
        {
            writer.WriteString(command.Name);
            writer.WriteInt32(command.CodeOffset);
        }

        writer.WriteString(string.Empty);
        writer.WriteInt32(module.Entries.Count);
        foreach (var entry in module.Entries)
        {
            writer.WriteInt32(entry);
        }

        foreach (var pointerReference in module.PointerReferences)
        {
            writer.WriteInt32(pointerReference);
        }

        writer.WriteInt32(-1);
        writer.WriteInt32(module.FixupProgram);
        writer.WriteInt32(module.FixupData);
        writer.WriteInt32(module.FixupTypes);
        writer.WriteInt32(module.BodyOffset);
        writer.WriteByte(module.Trailer);
        return writer.ToArray();
    }

    private static void ValidateModule(RscModule module)
    {
        ArgumentNullException.ThrowIfNull(module.Imports);
        ArgumentNullException.ThrowIfNull(module.Code);
        ArgumentNullException.ThrowIfNull(module.Commands);
        ArgumentNullException.ThrowIfNull(module.Entries);
        ArgumentNullException.ThrowIfNull(module.PointerReferences);
        ArgumentNullException.ThrowIfNull(module.TypeDescriptorPayload);
        ArgumentNullException.ThrowIfNull(module.StringPayload);
        RscModuleValidation.ValidateModuleName(module.Name);
        ValidateString(module.Name, nameof(module.Name), allowEmpty: false);
        if (module.Version != VersionKey)
        {
            throw new InvalidDataException($"unsupported RSC version: 0x{module.Version:x2}");
        }

        if (module.Size < 0)
        {
            throw new InvalidDataException($"invalid RSC size: {module.Size}");
        }

        if (module.VariableBytes < 0 || module.TypeDescriptorBytes < 0 || module.StringBytes < 0 || module.CodeWords < 0)
        {
            throw new InvalidDataException("RSC section length cannot be negative");
        }

        if (module.TypeDescriptorBytes != module.TypeDescriptorPayload.Length)
        {
            throw new InvalidDataException("RSC type descriptor byte count does not match its payload");
        }

        if (module.StringBytes != module.StringPayload.Length)
        {
            throw new InvalidDataException("RSC string byte count does not match its payload");
        }

        if (module.CodeWords != module.Code.Count)
        {
            throw new InvalidDataException("RSC code word count does not match its code");
        }

        if (module.Trailer != Trailer)
        {
            throw new InvalidDataException($"invalid RSC trailer: 0x{module.Trailer:x2}");
        }

        foreach (var import in module.Imports)
        {
            ArgumentNullException.ThrowIfNull(import);
            ValidateString(import.Name, "RSC import name", allowEmpty: false);
        }

        foreach (var command in module.Commands)
        {
            ArgumentNullException.ThrowIfNull(command);
            ValidateString(command.Name, "RSC command name", allowEmpty: false);
        }

        RscModuleValidation.ValidateUniqueNames(module.Imports, import => import.Name, "import");
        RscModuleValidation.ValidateUniqueNames(module.Commands, command => command.Name, "command");
        RscModuleValidation.ValidateEntryOffsets(module.CodeWords, module.Entries);
        RscModuleValidation.ValidateFixupHeads(
            module.CodeWords,
            module.TypeDescriptorBytes,
            module.FixupProgram,
            module.FixupData,
            module.FixupTypes);
        RscModuleValidation.ValidatePointerReferences(
            module.TypeDescriptorBytes,
            module.VariableBytes,
            module.StringBytes,
            module.PointerReferences);
        RscModuleValidation.ValidateDataLayout(
            module.TypeDescriptorBytes,
            module.VariableBytes,
            module.StringBytes);
        RscModuleValidation.ValidateCodeLayout(module.CodeWords, module.Commands, module.BodyOffset);
    }

    private static void ValidateString(string value, string name, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!allowEmpty && value.Length == 0)
        {
            throw new InvalidDataException($"{name} cannot be empty");
        }

        if (value.Contains('\0'))
        {
            throw new InvalidDataException($"{name} contains a NUL character");
        }

        foreach (var character in value)
        {
            if (character > 0x7f)
            {
                throw new InvalidDataException($"{name} must contain ASCII characters");
            }
        }
    }

    private sealed class BufferWriter
    {
        private readonly ArrayBufferWriter<byte> _data = new();

        public void WriteByte(byte value)
        {
            var buffer = _data.GetSpan(1);
            buffer[0] = value;
            _data.Advance(1);
        }

        public void WriteInt32(int value)
        {
            var buffer = _data.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            _data.Advance(sizeof(int));
        }

        public void WriteString(string value)
        {
            var byteCount = Encoding.ASCII.GetByteCount(value);
            var buffer = _data.GetSpan(byteCount + 1);
            Encoding.ASCII.GetBytes(value.AsSpan(), buffer);
            buffer[byteCount] = 0;
            _data.Advance(byteCount + 1);
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            var buffer = _data.GetSpan(bytes.Length);
            bytes.CopyTo(buffer);
            _data.Advance(bytes.Length);
        }

        public byte[] ToArray()
        {
            return _data.WrittenSpan.ToArray();
        }
    }
}
