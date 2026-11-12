using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ProjectOberon.Symbols;

public sealed record OberonSymbolExport(string Name, int Class, int Value)
{
    // Hand-built symbols historically treated class 1 exports as procedures.
    // The reader overrides this for constants, whose type is not a procedure.
    public bool? IsProcedure { get; init; }
    public bool? HasResult { get; init; }
    public int? ResultTypeForm { get; init; }
    public int? ResultBaseTypeForm { get; init; }
    public int? ResultBaseTypeSize { get; init; }
    public IReadOnlyList<OberonSymbolField>? ResultRecordFields { get; init; }
    public int ParameterCount { get; init; }
    public IReadOnlyList<bool>? ParameterReadOnly { get; init; }
    public IReadOnlyList<int>? ParameterTypeForms { get; init; }
    public IReadOnlyList<int?>? ParameterArrayLengths { get; init; }
    public IReadOnlyList<int?>? ParameterBaseTypeForms { get; init; }
    public IReadOnlyList<int?>? ParameterBaseTypeSizes { get; init; }
    public IReadOnlyList<IReadOnlyList<OberonSymbolField>?>? ParameterRecordFields { get; init; }
    public int? TypeForm { get; init; }
    public int? TypeSize { get; init; }
    public int? ArrayLength { get; init; }
    public int? BaseTypeForm { get; init; }
    public int? BaseTypeSize { get; init; }
    public IReadOnlyList<OberonSymbolField>? RecordFields { get; init; }
}

public sealed record OberonSymbolField(
    string Name,
    int Offset,
    int TypeForm,
    int? Size,
    int? ArrayLength,
    int? BaseTypeForm,
    int? BaseTypeSize,
    IReadOnlyList<OberonSymbolField>? RecordFields = null);

public sealed record OberonSymbolModule(
    string Name,
    int Key,
    byte Version,
    IReadOnlyList<OberonSymbolExport> Exports);

public static class OberonSymbolModuleReader
{
    private const byte VersionKey = 1;

    public static OberonSymbolModule Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SymbolReader(data);
        reader.ReadInt32();
        var key = reader.ReadInt32();
        var name = reader.ReadString();
        ValidateModuleName(name);
        var version = reader.ReadByte();
        if (version != VersionKey)
        {
            throw new InvalidDataException($"unsupported SMB version: 0x{version:x2}");
        }

        var exports = new List<OberonSymbolExport>();
        var exportNames = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var symbolClass = reader.ReadSignedByte();
            if (symbolClass == 0)
            {
                break;
            }

            var exportName = reader.ReadString();
            if (!exportNames.Add(exportName))
            {
                throw new InvalidDataException($"duplicate SMB export: {exportName}");
            }

            var typeInfo = SkipType(ref reader);
            var value = symbolClass is 1 or 2 ? reader.ReadNum() : 0;
            exports.Add(new OberonSymbolExport(exportName, symbolClass, value)
            {
                IsProcedure = typeInfo.IsProcedure,
                HasResult = typeInfo.HasResult,
                ResultTypeForm = typeInfo.ResultTypeForm,
                ResultBaseTypeForm = typeInfo.ResultBaseTypeForm,
                ResultBaseTypeSize = typeInfo.ResultBaseTypeSize,
                ResultRecordFields = typeInfo.ResultFields?.Select(MapField).ToArray(),
                ParameterCount = typeInfo.ParameterCount,
                ParameterReadOnly = typeInfo.ParameterReadOnly,
                ParameterTypeForms = typeInfo.Parameters?
                    .Select(parameter => parameter.TypeForm)
                    .ToArray(),
                ParameterArrayLengths = typeInfo.Parameters?
                    .Select(parameter => parameter.ArrayLength)
                    .ToArray(),
                ParameterBaseTypeForms = typeInfo.Parameters?
                    .Select(parameter => parameter.BaseTypeForm)
                    .ToArray(),
                ParameterBaseTypeSizes = typeInfo.Parameters?
                    .Select(parameter => parameter.BaseTypeSize)
                    .ToArray(),
                ParameterRecordFields = typeInfo.Parameters?
                    .Select(parameter => parameter.Fields?.Select(MapField).ToArray())
                    .ToArray(),
                TypeForm = typeInfo.TypeForm,
                TypeSize = typeInfo.Size,
                ArrayLength = typeInfo.ArrayLength,
                BaseTypeForm = typeInfo.BaseTypeForm,
                BaseTypeSize = typeInfo.BaseTypeSize,
                RecordFields = typeInfo.Fields?.Select(MapField).ToArray()
            });

            if (symbolClass == 5)
            {
                SkipTypeFixups(ref reader);
            }
        }

        reader.EnsureEnd();
        return new OberonSymbolModule(name, key, version, exports);
    }

    private static SymbolTypeInfo SkipType(ref SymbolReader reader)
    {
        var reference = reader.ReadSignedByte();
        if (reference < 0)
        {
            return new SymbolTypeInfo(false, 0, false, -1, null, null, null, null, null, null, null, null, null, null, null);
        }

        var form = reader.ReadSignedByte();
        var isProcedure = form == 10;
        var parameterCount = 0;
        IReadOnlyList<bool>? parameterReadOnly = null;
        IReadOnlyList<SymbolParameterInfo>? parameters = null;
        int? arrayLength = null;
        int? baseTypeForm = null;
        int? baseTypeSize = null;
        int? typeSize = null;
        IReadOnlyList<SymbolFieldInfo>? fields = null;
        int? resultTypeForm = null;
        int? resultBaseTypeForm = null;
        int? resultBaseTypeSize = null;
        IReadOnlyList<SymbolFieldInfo>? resultFields = null;
        var hasResult = false;
        switch (form)
        {
            case 7:
                var baseType = SkipType(ref reader);
                baseTypeForm = baseType.TypeForm;
                baseTypeSize = baseType.Size;
                break;
            case 12:
                var arrayBaseType = SkipType(ref reader);
                baseTypeForm = arrayBaseType.TypeForm;
                baseTypeSize = arrayBaseType.Size;
                arrayLength = reader.ReadNum();
                typeSize = reader.ReadNum();
                break;
            case 13:
                SkipType(ref reader);
                reader.ReadNum();
                reader.ReadNum();
                typeSize = reader.ReadNum();
                fields = ReadRecordFields(ref reader);
                break;
            case 10:
                var resultType = SkipType(ref reader);
                hasResult = resultType.TypeForm != 9;
                resultTypeForm = resultType.TypeForm;
                resultBaseTypeForm = resultType.BaseTypeForm;
                resultBaseTypeSize = resultType.BaseTypeSize;
                resultFields = resultType.Fields;
                parameters = SkipProcedureParameters(ref reader);
                parameterReadOnly = parameters
                    .Select(parameter => parameter.ReadOnly)
                    .ToArray();
                parameterCount = parameterReadOnly.Count;
                break;
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 8:
            case 9:
            case 11:
                break;
            default:
                throw new InvalidDataException($"invalid SMB type form: {form}");
        }

        var reexportedModule = reader.ReadString();
        if (reexportedModule.Length > 0)
        {
            reader.ReadInt32();
            reader.ReadString();
        }

        return new SymbolTypeInfo(
            isProcedure,
            parameterCount,
            hasResult,
            form,
            parameterReadOnly,
            arrayLength,
            parameters,
            resultTypeForm,
            baseTypeForm,
            baseTypeSize,
            typeSize,
            fields,
            resultBaseTypeForm,
            resultBaseTypeSize,
            resultFields);
    }

    private static IReadOnlyList<SymbolFieldInfo> ReadRecordFields(ref SymbolReader reader)
    {
        var fields = new List<SymbolFieldInfo>();
        while (true)
        {
            var fieldClass = reader.ReadSignedByte();
            if (fieldClass == 0)
            {
                return fields;
            }

            var fieldName = reader.ReadString();
            if (fieldName.Length == 0)
            {
                reader.ReadNum();
                continue;
            }

            var fieldType = SkipType(ref reader);
            var offset = reader.ReadNum();
            fields.Add(new SymbolFieldInfo(
                fieldName,
                offset,
                fieldType.TypeForm,
                fieldType.Size,
                fieldType.ArrayLength,
                fieldType.BaseTypeForm,
                fieldType.BaseTypeSize,
                fieldType.Fields));
        }
    }

    private static OberonSymbolField MapField(SymbolFieldInfo field) => new(
        field.Name,
        field.Offset,
        field.TypeForm,
        field.Size,
        field.ArrayLength,
        field.BaseTypeForm,
        field.BaseTypeSize,
        field.Fields?.Select(MapField).ToArray());

    private static IReadOnlyList<SymbolParameterInfo> SkipProcedureParameters(ref SymbolReader reader)
    {
        var parameters = new List<SymbolParameterInfo>();
        while (true)
        {
            var parameterClass = reader.ReadSignedByte();
            if (parameterClass == 0)
            {
                return parameters;
            }

            var readOnly = reader.ReadSignedByte() != 0;
            var type = SkipType(ref reader);
            parameters.Add(new SymbolParameterInfo(
                readOnly,
                type.TypeForm,
                type.ArrayLength,
                type.BaseTypeForm,
                type.BaseTypeSize,
                type.Fields));
        }
    }

    private static void SkipTypeFixups(ref SymbolReader reader)
    {
        while (reader.ReadSignedByte() != 0)
        {
        }
    }

    private static void ValidateModuleName(string name)
    {
        if (name.Length == 0 || name.Length >= 32)
        {
            throw new InvalidDataException("SMB module name must be between 1 and 31 characters");
        }
    }

    private readonly record struct SymbolTypeInfo(
        bool IsProcedure,
        int ParameterCount,
        bool HasResult,
        int TypeForm,
        IReadOnlyList<bool>? ParameterReadOnly,
        int? ArrayLength,
        IReadOnlyList<SymbolParameterInfo>? Parameters,
        int? ResultTypeForm,
        int? BaseTypeForm,
        int? BaseTypeSize,
        int? Size,
        IReadOnlyList<SymbolFieldInfo>? Fields,
        int? ResultBaseTypeForm,
        int? ResultBaseTypeSize,
        IReadOnlyList<SymbolFieldInfo>? ResultFields);

    private readonly record struct SymbolParameterInfo(
        bool ReadOnly,
        int TypeForm,
        int? ArrayLength,
        int? BaseTypeForm,
        int? BaseTypeSize,
        IReadOnlyList<SymbolFieldInfo>? Fields);

    private readonly record struct SymbolFieldInfo(
        string Name,
        int Offset,
        int TypeForm,
        int? Size,
        int? ArrayLength,
        int? BaseTypeForm,
        int? BaseTypeSize,
        IReadOnlyList<SymbolFieldInfo>? Fields);

    private ref struct SymbolReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public SymbolReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _data[_position++];
        }

        public int ReadSignedByte()
        {
            var value = ReadByte();
            return value < 0x80 ? value : value - 0x100;
        }

        public int ReadInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadInt32LittleEndian(_data[_position..]);
            _position += 4;
            return value;
        }

        public int ReadNum()
        {
            var bits = 32;
            uint value = 0;
            var next = ReadByte();
            var continuationCount = 0;
            while (next >= 0x80)
            {
                continuationCount++;
                if (continuationCount > 4)
                {
                    throw new InvalidDataException("SMB number is too long");
                }

                value = BitOperations.RotateRight(value + next - 0x80u, 7);
                bits -= 7;
                next = ReadByte();
            }

            if (bits <= 4)
            {
                return unchecked((int)BitOperations.RotateRight(value + (uint)(next & 0x0f), 4));
            }

            var rotated = unchecked((int)BitOperations.RotateRight(value + next, 7));
            return rotated >> (bits - 7);
        }

        public string ReadString()
        {
            var start = _position;
            while (true)
            {
                if (_position >= _data.Length)
                {
                    throw new InvalidDataException("unterminated SMB string");
                }

                if (ReadByte() == 0)
                {
                    return Encoding.ASCII.GetString(_data[start..(_position - 1)]);
                }

                if (_data[_position - 1] > 0x7f)
                {
                    throw new InvalidDataException("SMB strings must contain ASCII characters");
                }
            }
        }

        private void Ensure(int count)
        {
            if (count < 0 || count > _data.Length - _position)
            {
                throw new InvalidDataException("truncated SMB module");
            }
        }

        public void EnsureEnd()
        {
            var remaining = _data.Length - _position;
            if (remaining > 3)
            {
                throw new InvalidDataException("unexpected trailing bytes in SMB module");
            }

            for (var index = _position; index < _data.Length; index++)
            {
                if (_data[index] != 0)
                {
                    throw new InvalidDataException("unexpected trailing bytes in SMB module");
                }
            }
        }
    }
}
