using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ProjectOberon.Compiler;

public static class OberonCompilerSymbolFileWriter
{
    private const int VersionKey = 1;
    private const int FirstTypeReference = 14;

    public static byte[] Write(string moduleName, IReadOnlyList<OberonCompilerObject> exports)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(exports);
        ValidateName(moduleName, "module name", allowEmpty: false, maxLength: 31);

        var writer = new SymbolFileWriter();
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteString(moduleName);
        writer.WriteByte(VersionKey);

        var types = new TypeWriter(writer);
        foreach (var export in exports)
        {
            ArgumentNullException.ThrowIfNull(export);

            if (!export.Exported)
            {
                throw new ArgumentException($"symbol is not exported: {export.Name}", nameof(exports));
            }

            if (export.Type is null)
            {
                throw new ArgumentException($"symbol has no type: {export.Name}", nameof(exports));
            }

            ValidateName(export.Name, "symbol name", allowEmpty: false, maxLength: 31);

            writer.WriteByte((byte)export.Class);
            writer.WriteString(export.Name);
            types.WriteType(export.Type);
            switch (export.Class)
            {
                case OberonCompilerObjectClass.Type:
                    writer.WriteByte(0);
                    break;
                case OberonCompilerObjectClass.Constant:
                    if (export.Type.Form == OberonCompilerTypeForm.Real)
                        writer.WriteInt(checked((int)export.Value));
                    else
                        writer.WriteNum(checked((int)export.Value));
                    break;
                case OberonCompilerObjectClass.Variable:
                    writer.WriteNum(checked((int)export.Value));
                    break;
            }
        }

        writer.WriteByte(0);
        writer.Align4();
        var data = writer.ToArray();
        var checksum = 0;
        for (var offset = 0; offset < data.Length; offset += sizeof(int))
            checksum = unchecked(checksum + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, sizeof(int))));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(sizeof(int)), checksum);
        return data;
    }

    private static void ValidateName(string value, string description, bool allowEmpty, int maxLength)
    {
        if ((!allowEmpty && value.Length == 0) || value.Length > maxLength)
        {
            throw new ArgumentException($"{description} must contain 1 to {maxLength} characters");
        }

        if (value.Contains('\0') || value.Any(character => character > 0x7f))
        {
            throw new ArgumentException($"{description} must contain ASCII characters");
        }
    }

    private sealed class TypeWriter
    {
        private readonly SymbolFileWriter _writer;
        private readonly Dictionary<OberonCompilerType, int> _references = [];
        private int _nextReference = FirstTypeReference;

        public TypeWriter(SymbolFileWriter writer) => _writer = writer;

        public void WriteType(OberonCompilerType type)
        {
            if (type.TypeObject is not null && _references.TryGetValue(type, out var reference))
            {
                _writer.WriteByte(unchecked((byte)-reference));
                return;
            }

            if (type.TypeObject is not null)
            {
                if (_nextReference > 63)
                {
                    throw new InvalidDataException("too many exported symbol types");
                }
                reference = _nextReference++;
                _references[type] = reference;
                _writer.WriteByte((byte)reference);
            }
            else
            {
                _writer.WriteByte(0);
            }

            _writer.WriteByte((byte)type.Form);
            switch (type.Form)
            {
                case OberonCompilerTypeForm.Pointer:
                    WriteType(type.Base ?? throw MissingBase(type));
                    break;
                case OberonCompilerTypeForm.Array:
                    WriteType(type.Base ?? throw MissingBase(type));
                    _writer.WriteNum(type.Length);
                    _writer.WriteNum(type.Size);
                    break;
                case OberonCompilerTypeForm.Record:
                    WriteRecord(type);
                    break;
                case OberonCompilerTypeForm.Procedure:
                    WriteProcedure(type);
                    break;
            }

            if (type.ModuleNumber > 0 && type.TypeObject is not null)
            {
                _writer.WriteString(type.TypeObject.OriginalName ?? string.Empty);
                _writer.WriteInt(type.TypeObject.Key);
                _writer.WriteString(type.TypeObject.Name);
            }
            else
            {
                _writer.WriteString(string.Empty);
            }
        }

        private void WriteRecord(OberonCompilerType type)
        {
            WriteType(type.Base ?? new OberonCompilerType(OberonCompilerTypeForm.None, 4));
            _writer.WriteNum(type.TypeObject is null ? 0 : checked((int)type.TypeObject.Value));
            _writer.WriteNum(type.ParameterCount);
            _writer.WriteNum(type.Size);
            foreach (var field in type.Fields)
            {
                if (field.Exported)
                {
                    _writer.WriteByte((byte)OberonCompilerObjectClass.Field);
                    _writer.WriteString(field.Name);
                    WriteType(field.Type ?? throw MissingType(field));
                    _writer.WriteNum(checked((int)field.Value));
                }
                else
                {
                    WriteHiddenPointers(field.Type, field.Value);
                }
            }
            _writer.WriteByte(0);
        }

        private void WriteHiddenPointers(OberonCompilerType? type, long offset)
        {
            OberonCompilerTypeWalker.VisitPointerLocations(
                type,
                offset,
                pointerOffset =>
                {
                    _writer.WriteByte((byte)OberonCompilerObjectClass.Field);
                    _writer.WriteString(string.Empty);
                    _writer.WriteNum(checked((int)pointerOffset));
                }
            );
        }

        private void WriteProcedure(OberonCompilerType type)
        {
            WriteType(type.Base ?? new OberonCompilerType(OberonCompilerTypeForm.None, 4));
            foreach (var parameter in type.Parameters)
            {
                _writer.WriteByte((byte)parameter.Class);
                _writer.WriteByte(parameter.ReadOnly ? 1 : 0);
                WriteType(parameter.Type ?? throw MissingType(parameter));
            }
            _writer.WriteByte(0);
        }

        private static InvalidDataException MissingBase(OberonCompilerType type) =>
            new($"{type.Form} type has no base type");

        private static InvalidDataException MissingType(OberonCompilerObject obj) =>
            new($"symbol has no type: {obj.Name}");
    }

    private sealed class SymbolFileWriter
    {
        private readonly ArrayBufferWriter<byte> _data = new();

        public void WriteByte(int value)
        {
            var buffer = _data.GetSpan(1);
            buffer[0] = checked((byte)value);
            _data.Advance(1);
        }

        public void WriteInt(int value)
        {
            var buffer = _data.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            _data.Advance(sizeof(int));
        }

        public void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Contains('\0') || value.Any(character => character > 0x7f))
            {
                throw new ArgumentException("SMB strings must contain ASCII characters", nameof(value));
            }

            var byteCount = Encoding.ASCII.GetByteCount(value);
            var buffer = _data.GetSpan(byteCount + 1);
            Encoding.ASCII.GetBytes(value.AsSpan(), buffer);
            buffer[byteCount] = 0;
            _data.Advance(byteCount + 1);
        }

        public void WriteNum(int value)
        {
            while (value < -0x40 || value >= 0x40)
            {
                WriteByte((value & 0x7f) | 0x80);
                value >>= 7;
            }
            WriteByte(value & 0x7f);
        }

        public void Align4()
        {
            while (_data.WrittenCount % sizeof(int) != 0)
            {
                WriteByte(0);
            }
        }

        public byte[] ToArray()
        {
            return _data.WrittenSpan.ToArray();
        }
    }
}
