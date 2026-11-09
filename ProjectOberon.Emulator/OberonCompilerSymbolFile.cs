using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ProjectOberon.Emulator;

public static class OberonCompilerSymbolFileWriter
{
    private const int VersionKey = 1;
    private const int FirstTypeReference = 14;

    public static byte[] Write(string moduleName, IReadOnlyList<OberonCompilerObject> exports)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(exports);

        var writer = new SymbolFileWriter();
        writer.WriteInt(0);
        writer.WriteInt(0);
        writer.WriteString(moduleName);
        writer.WriteByte(VersionKey);

        var types = new TypeWriter(writer);
        foreach (var export in exports)
        {
            if (!export.Exported) throw new ArgumentException($"symbol is not exported: {export.Name}", nameof(exports));
            if (export.Type is null) throw new ArgumentException($"symbol has no type: {export.Name}", nameof(exports));

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
                if (_nextReference > 63) throw new InvalidDataException("too many exported symbol types");
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
            if (type is null) return;
            if (type.Form is OberonCompilerTypeForm.Pointer or OberonCompilerTypeForm.Nil)
            {
                _writer.WriteByte((byte)OberonCompilerObjectClass.Field);
                _writer.WriteString(string.Empty);
                _writer.WriteNum(checked((int)offset));
                return;
            }

            if (type.Form == OberonCompilerTypeForm.Record)
            {
                foreach (var field in type.Fields) WriteHiddenPointers(field.Type, offset + field.Value);
            }
            else if (type.Form == OberonCompilerTypeForm.Array && type.Length >= 0 && type.Base is not null)
            {
                for (var index = 0; index < type.Length; index++)
                    WriteHiddenPointers(type.Base, offset + (long)type.Base.Size * index);
            }
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
        private readonly List<byte> _data = [];

        public void WriteByte(int value) => _data.Add(checked((byte)value));

        public void WriteInt(int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            _data.AddRange(bytes.ToArray());
        }

        public void WriteString(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            _data.AddRange(bytes);
            WriteByte(0);
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
            while (_data.Count % sizeof(int) != 0) WriteByte(0);
        }

        public byte[] ToArray() => _data.ToArray();
    }
}
