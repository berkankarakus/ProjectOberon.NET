using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace ProjectOberon.Symbols;

public sealed record OberonSymbolExport(string Name, int Class, int Value)
{
    // Hand-built symbols historically treated class 1 exports as procedures.
    // The reader overrides this for constants, whose type is not a procedure.
    public bool? IsProcedure { get; init; }
}

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
        while (true)
        {
            var symbolClass = reader.ReadSignedByte();
            if (symbolClass == 0)
            {
                break;
            }

            var exportName = reader.ReadString();
            var isProcedure = SkipType(ref reader);
            var value = symbolClass is 1 or 2 ? reader.ReadNum() : 0;
            exports.Add(new OberonSymbolExport(exportName, symbolClass, value)
            {
                IsProcedure = isProcedure
            });

            if (symbolClass == 5)
            {
                SkipTypeFixups(ref reader);
            }
        }

        reader.EnsureEnd();
        return new OberonSymbolModule(name, key, version, exports);
    }

    private static bool SkipType(ref SymbolReader reader)
    {
        var reference = reader.ReadSignedByte();
        if (reference < 0)
        {
            return false;
        }

        var form = reader.ReadSignedByte();
        var isProcedure = form == 10;
        switch (form)
        {
            case 7:
                SkipType(ref reader);
                break;
            case 12:
                SkipType(ref reader);
                reader.ReadNum();
                reader.ReadNum();
                break;
            case 13:
                SkipType(ref reader);
                reader.ReadNum();
                reader.ReadNum();
                reader.ReadNum();
                SkipRecordFields(ref reader);
                break;
            case 10:
                SkipType(ref reader);
                SkipProcedureParameters(ref reader);
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

        return isProcedure;
    }

    private static void SkipRecordFields(ref SymbolReader reader)
    {
        while (true)
        {
            var fieldClass = reader.ReadSignedByte();
            if (fieldClass == 0)
            {
                return;
            }

            var fieldName = reader.ReadString();
            if (fieldName.Length > 0)
            {
                SkipType(ref reader);
            }

            reader.ReadNum();
        }
    }

    private static void SkipProcedureParameters(ref SymbolReader reader)
    {
        while (true)
        {
            var parameterClass = reader.ReadSignedByte();
            if (parameterClass == 0)
            {
                return;
            }

            reader.ReadSignedByte();
            SkipType(ref reader);
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
            while (next >= 0x80)
            {
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
