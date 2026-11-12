namespace ProjectOberon.Compiler;

public enum OberonCompilerObjectClass
{
    Head,
    Constant,
    Variable,
    Parameter,
    Field,
    Type,
    Procedure,
    Function,
    Module
}

public enum OberonCompilerTypeForm
{
    Byte = 1,
    Boolean,
    Character,
    Integer,
    Real,
    Set,
    Pointer,
    Nil,
    None,
    Procedure,
    String,
    Array,
    Record
}

public sealed record OberonCompilerSymbolDiagnostic(string Message, string Name);

public sealed class OberonCompilerType
{
    internal OberonCompilerType(OberonCompilerTypeForm form, int size)
    {
        Form = form;
        Size = size;
    }

    public OberonCompilerTypeForm Form { get; }
    public int Reference { get; internal set; }
    public int ModuleNumber { get; internal set; }
    public int ParameterCount { get; internal set; }
    public int Length { get; internal set; }
    public int Size { get; internal set; }
    public OberonCompilerType? Base { get; internal set; }
    public OberonCompilerObject? TypeObject { get; internal set; }
    public IReadOnlyList<OberonCompilerObject> Fields => _fields;
    public IReadOnlyList<OberonCompilerObject> Parameters => _parameters;

    private readonly List<OberonCompilerObject> _fields = [];
    private readonly List<OberonCompilerObject> _parameters = [];

    internal void AddField(OberonCompilerObject field) => _fields.Add(field);
    internal void AddParameter(OberonCompilerObject parameter) => _parameters.Add(parameter);
}

public sealed class OberonCompilerObject
{
    internal OberonCompilerObject(string name, OberonCompilerObjectClass @class)
    {
        Name = name;
        Class = @class;
    }

    public string Name { get; }
    public OberonCompilerObjectClass Class { get; }
    public bool Exported { get; internal set; }
    public bool ReadOnly { get; internal set; }
    public int Level { get; internal set; }
    public long Value { get; internal set; }
    public OberonCompilerType? Type { get; internal set; }
    public string? OriginalName { get; internal set; }
    public int Key { get; internal set; }
    public IReadOnlyList<OberonCompilerObject> Members => _members;

    private readonly List<OberonCompilerObject> _members = [];
    private readonly Dictionary<string, OberonCompilerObject> _memberMap = new(StringComparer.Ordinal);

    public OberonCompilerObject? FindMember(string name) =>
        _memberMap.TryGetValue(name, out var member) ? member : null;

    internal bool AddMember(OberonCompilerObject member)
    {
        if (!_memberMap.TryAdd(member.Name, member))
        {
            return false;
        }
        _members.Add(member);
        return true;
    }
}

public sealed class OberonCompilerScope
{
    internal OberonCompilerScope(OberonCompilerScope? parent, int level)
    {
        Parent = parent;
        Level = level;
    }

    public OberonCompilerScope? Parent { get; }
    public int Level { get; }
    public IReadOnlyList<OberonCompilerObject> Objects => _objects;

    private readonly List<OberonCompilerObject> _objects = [];
    private readonly Dictionary<string, OberonCompilerObject> _objectMap = new(StringComparer.Ordinal);

    internal bool TryAdd(OberonCompilerObject obj)
    {
        if (!_objectMap.TryAdd(obj.Name, obj))
        {
            return false;
        }
        _objects.Add(obj);
        return true;
    }

    internal OberonCompilerObject? FindLocal(string name) =>
        _objectMap.TryGetValue(name, out var obj) ? obj : null;
}

public sealed class OberonCompilerSymbols
{
    private readonly List<OberonCompilerSymbolDiagnostic> _diagnostics = [];
    private OberonCompilerScope _currentScope;

    public OberonCompilerSymbols()
    {
        Universe = new OberonCompilerScope(parent: null, level: 0);
        _currentScope = Universe;

        ByteType = CreateType(OberonCompilerTypeForm.Byte, 1);
        BooleanType = CreateType(OberonCompilerTypeForm.Boolean, 1);
        CharacterType = CreateType(OberonCompilerTypeForm.Character, 1);
        IntegerType = CreateType(OberonCompilerTypeForm.Integer, 4);
        RealType = CreateType(OberonCompilerTypeForm.Real, 4);
        SetType = CreateType(OberonCompilerTypeForm.Set, 4);
        NilType = CreateType(OberonCompilerTypeForm.Nil, 4);
        NoneType = CreateType(OberonCompilerTypeForm.None, 4);
        StringType = CreateType(OberonCompilerTypeForm.String, 8);

        EnterBuiltinType("SET", SetType);
        EnterBuiltinType("BOOLEAN", BooleanType);
        EnterBuiltinType("BYTE", ByteType);
        EnterBuiltinType("CHAR", CharacterType);
        EnterBuiltinType("LONGREAL", RealType);
        EnterBuiltinType("REAL", RealType);
        EnterBuiltinType("LONGINT", IntegerType);
        EnterBuiltinType("INTEGER", IntegerType);

        SystemModule = new OberonCompilerObject("SYSTEM", OberonCompilerObjectClass.Module)
        {
            OriginalName = "SYSTEM",
            Type = NoneType,
            ReadOnly = true
        };
        EnterSystemBuiltins();
    }

    public OberonCompilerScope Universe { get; }
    public OberonCompilerScope CurrentScope => _currentScope;
    public OberonCompilerObject SystemModule { get; }
    public IReadOnlyList<OberonCompilerSymbolDiagnostic> Diagnostics => _diagnostics;

    public OberonCompilerType ByteType { get; }
    public OberonCompilerType BooleanType { get; }
    public OberonCompilerType CharacterType { get; }
    public OberonCompilerType IntegerType { get; }
    public OberonCompilerType RealType { get; }
    public OberonCompilerType SetType { get; }
    public OberonCompilerType NilType { get; }
    public OberonCompilerType NoneType { get; }
    public OberonCompilerType StringType { get; }

    public OberonCompilerType CreateType(OberonCompilerTypeForm form, int size) =>
        new(form, size);

    public OberonCompilerType CreatePointer(OberonCompilerType baseType) =>
        new(OberonCompilerTypeForm.Pointer, 4) { Base = baseType };

    public OberonCompilerType CreateArray(OberonCompilerType baseType, int length, int size) =>
        new(OberonCompilerTypeForm.Array, size) { Base = baseType, Length = length };

    public OberonCompilerType CreateRecord(OberonCompilerType? baseType = null, int extensionLevel = 0) =>
        new(OberonCompilerTypeForm.Record, baseType?.Size ?? 0)
        {
            Base = baseType,
            ParameterCount = extensionLevel
        };

    public OberonCompilerType CreateProcedure(OberonCompilerType resultType,
        IEnumerable<OberonCompilerObject> parameters)
    {
        var type = new OberonCompilerType(OberonCompilerTypeForm.Procedure, 4)
        {
            Base = resultType
        };
        foreach (var parameter in parameters)
        {
            type.AddParameter(parameter);
        }
        type.ParameterCount = type.Parameters.Count;
        return type;
    }

    public OberonCompilerObject Declare(
        string name,
        OberonCompilerObjectClass @class,
        OberonCompilerType? type = null,
        long value = 0,
        bool exported = false,
        bool readOnly = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var existing = _currentScope.FindLocal(name);
        if (existing is not null)
        {
            AddDiagnostic("multiple definition", name);
            return existing;
        }

        var obj = new OberonCompilerObject(name, @class)
        {
            Type = type,
            Value = value,
            Exported = exported,
            ReadOnly = readOnly,
            Level = _currentScope.Level
        };
        _currentScope.TryAdd(obj);
        if (@class == OberonCompilerObjectClass.Type && type is not null) type.TypeObject = obj;
        return obj;
    }

    public OberonCompilerObject DeclareField(
        OberonCompilerType recordType,
        string name,
        OberonCompilerType fieldType,
        int offset,
        bool exported = false)
    {
        if (recordType.Form != OberonCompilerTypeForm.Record)
            throw new ArgumentException("fields can only be declared on record types", nameof(recordType));

        var existing = recordType.Fields.FirstOrDefault(field => field.Name == name);
        if (existing is not null)
        {
            AddDiagnostic("multiple field definition", name);
            return existing;
        }

        var field = new OberonCompilerObject(name, OberonCompilerObjectClass.Field)
        {
            Type = fieldType,
            Value = offset,
            Exported = exported
        };
        recordType.AddField(field);
        return field;
    }

    public OberonCompilerObject? Lookup(string name)
    {
        for (var scope = _currentScope; scope is not null; scope = scope.Parent)
        {
            var result = scope.FindLocal(name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    public OberonCompilerObject? LookupField(OberonCompilerType recordType, string name)
    {
        for (var type = recordType; type is not null; type = type.Base)
        {
            var field = type.Fields.FirstOrDefault(candidate => candidate.Name == name);
            if (field is not null)
            {
                return field;
            }
        }

        return null;
    }

    public OberonCompilerObject OpenModule(string name, string originalName, int key)
    {
        var module = Declare(name, OberonCompilerObjectClass.Module, NoneType);
        module.OriginalName = originalName;
        module.Key = key;
        module.ReadOnly = true;
        return module;
    }

    public OberonCompilerObject ImportModule(string alias, OberonSymbolModule module)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);
        ArgumentNullException.ThrowIfNull(module);

        var imported = OpenModule(alias, module.Name, module.Key);
        foreach (var export in module.Exports.Where(export => export.IsProcedure == true))
        {
            var hasResult = export.HasResult == true;
            var importedClass = hasResult
                ? OberonCompilerObjectClass.Function
                : OberonCompilerObjectClass.Procedure;
            var resultType = NoneType;
            if (hasResult)
            {
                var importedResultType = CreateImportedPrimitiveType(export.ResultTypeForm);
                if (importedResultType is null
                    && export.ResultTypeForm == (int)OberonCompilerTypeForm.Pointer)
                {
                    importedResultType = CreateImportedPointerType(
                        export.ResultBaseTypeForm,
                        export.ResultBaseTypeSize);
                }

                resultType = importedResultType is null ? IntegerType : importedResultType;
            }
            var procedure = new OberonCompilerObject(export.Name, importedClass)
            {
                Type = CreateProcedure(
                    resultType,
                    Enumerable.Range(0, export.ParameterCount)
                        .Select(index => DeclareImportedParameter(
                            export.Name,
                            index,
                            export.ParameterReadOnly,
                            export.ParameterTypeForms,
                            export.ParameterArrayLengths,
                            export.ParameterBaseTypeForms,
                            export.ParameterBaseTypeSizes))),
                Value = export.Value,
                ReadOnly = true,
                OriginalName = export.Name,
                Key = module.Key
            };
            imported.AddMember(procedure);
        }

        foreach (var export in module.Exports.Where(
            export => export.IsProcedure == false && export.Class == (int)OberonCompilerObjectClass.Constant))
        {
            var constant = new OberonCompilerObject(export.Name, OberonCompilerObjectClass.Constant)
            {
                Type = IntegerType,
                Value = export.Value,
                ReadOnly = true,
                OriginalName = export.Name,
                Key = module.Key
            };
            imported.AddMember(constant);
        }

        foreach (var export in module.Exports.Where(
            export => export.IsProcedure != true && export.Class == (int)OberonCompilerObjectClass.Variable))
        {
            var variable = new OberonCompilerObject(export.Name, OberonCompilerObjectClass.Variable)
            {
                Type = IntegerType,
                Value = export.Value,
                OriginalName = export.Name,
                Key = module.Key,
                Level = 0
            };
            imported.AddMember(variable);
        }

        foreach (var export in module.Exports.Where(
            export => export.IsProcedure != true && export.Class == (int)OberonCompilerObjectClass.Type))
        {
            var importedType = CreateImportedPrimitiveType(export.TypeForm);
            if (importedType is null
                && export.TypeForm == (int)OberonCompilerTypeForm.Array)
            {
                importedType = CreateImportedArrayType(
                    export.ArrayLength,
                    export.BaseTypeForm,
                    export.BaseTypeSize,
                    export.TypeSize);
            }
            if (importedType is null
                && export.TypeForm == (int)OberonCompilerTypeForm.Record)
            {
                importedType = CreateImportedRecordType(export.TypeSize, export.RecordFields);
            }

            if (importedType is null)
            {
                continue;
            }

            var type = new OberonCompilerObject(export.Name, OberonCompilerObjectClass.Type)
            {
                Type = importedType,
                ReadOnly = true,
                OriginalName = export.Name,
                Key = module.Key
            };
            imported.AddMember(type);
        }

        return imported;
    }

    private OberonCompilerType? CreateImportedPrimitiveType(int? typeForm)
    {
        if (typeForm is not int form)
        {
            return null;
        }

        return form switch
        {
            (int)OberonCompilerTypeForm.Byte => ByteType,
            (int)OberonCompilerTypeForm.Boolean => BooleanType,
            (int)OberonCompilerTypeForm.Character => CharacterType,
            (int)OberonCompilerTypeForm.Integer => IntegerType,
            (int)OberonCompilerTypeForm.Real => RealType,
            (int)OberonCompilerTypeForm.Set => SetType,
            (int)OberonCompilerTypeForm.Nil => NilType,
            (int)OberonCompilerTypeForm.None => NoneType,
            (int)OberonCompilerTypeForm.String => StringType,
            _ => null
        };
    }

    private OberonCompilerType? CreateImportedRecordType(
        int? typeSize,
        IReadOnlyList<OberonSymbolField>? fields = null)
    {
        if (typeSize is not int recordSize)
        {
            return null;
        }

        var recordType = CreateRecord();
        recordType.Size = recordSize;
        foreach (var field in fields ?? [])
        {
            var fieldType = CreateImportedFieldType(field);
            if (fieldType is not null)
            {
                DeclareField(recordType, field.Name, fieldType, field.Offset, exported: true);
            }
        }
        return recordType;
    }

    private OberonCompilerType? CreateImportedFieldType(OberonSymbolField field)
    {
        if (field.TypeForm == (int)OberonCompilerTypeForm.Array
            && field.ArrayLength is int arrayLength
            && field.BaseTypeForm is int baseTypeForm)
        {
            var baseType = CreateImportedPrimitiveType(baseTypeForm);
            if (baseType is null)
            {
                return null;
            }

            var size = field.Size ?? checked(baseType.Size * Math.Max(arrayLength, 0));
            return CreateArray(baseType, arrayLength, size);
        }

        if (field.TypeForm == (int)OberonCompilerTypeForm.Pointer)
        {
            var baseType = CreateImportedPrimitiveType(field.BaseTypeForm);
            if (baseType is not null)
            {
                return CreatePointer(baseType);
            }

            if (field.BaseTypeForm == (int)OberonCompilerTypeForm.Record)
            {
                var recordType = CreateImportedRecordType(field.BaseTypeSize, field.RecordFields);
                if (recordType is not null)
                {
                    return CreatePointer(recordType);
                }
            }

            return null;
        }

        if (field.TypeForm == (int)OberonCompilerTypeForm.Record)
        {
            return CreateImportedRecordType(field.Size, field.RecordFields);
        }

        return CreateImportedPrimitiveType(field.TypeForm);
    }

    private OberonCompilerType? CreateImportedArrayType(
        int? length,
        int? baseTypeForm,
        int? baseTypeSize,
        int? typeSize)
    {
        if (length is not int arrayLength || baseTypeForm is not int elementForm)
        {
            return null;
        }

        var elementType = CreateImportedPrimitiveType(elementForm);
        if (elementType is null && elementForm == (int)OberonCompilerTypeForm.Record)
        {
            elementType = CreateImportedRecordType(baseTypeSize);
        }
        if (elementType is null)
        {
            return null;
        }

        var size = typeSize ?? (arrayLength < 0
            ? 8
            : checked(elementType.Size * arrayLength));
        return CreateArray(elementType, arrayLength, size);
    }

    private OberonCompilerType? CreateImportedPointerType(
        int? baseTypeForm,
        int? baseTypeSize)
    {
        if (baseTypeForm != (int)OberonCompilerTypeForm.Record)
        {
            return null;
        }

        var recordType = CreateImportedRecordType(baseTypeSize);
        if (recordType is null)
        {
            return null;
        }

        return CreatePointer(recordType);
    }

    private OberonCompilerObject DeclareImportedParameter(
        string procedureName,
        int index,
        IReadOnlyList<bool>? parameterReadOnly,
        IReadOnlyList<int>? parameterTypeForms,
        IReadOnlyList<int?>? parameterArrayLengths,
        IReadOnlyList<int?>? parameterBaseTypeForms,
        IReadOnlyList<int?>? parameterBaseTypeSizes)
    {
        var isReadOnly = true;
        if (parameterReadOnly is not null && index < parameterReadOnly.Count)
        {
            isReadOnly = parameterReadOnly[index];
        }

        var type = IntegerType;
        if (parameterTypeForms is not null && index < parameterTypeForms.Count)
        {
            var typeForm = parameterTypeForms[index];
            var arrayLength = parameterArrayLengths is not null && index < parameterArrayLengths.Count
                ? parameterArrayLengths[index]
                : null;
            var baseTypeForm = parameterBaseTypeForms is not null && index < parameterBaseTypeForms.Count
                ? parameterBaseTypeForms[index]
                : null;
            var baseTypeSize = parameterBaseTypeSizes is not null && index < parameterBaseTypeSizes.Count
                ? parameterBaseTypeSizes[index]
                : null;
            if (typeForm == (int)OberonCompilerTypeForm.Array)
            {
                type = CreateImportedArrayType(
                    arrayLength,
                    baseTypeForm,
                    baseTypeSize,
                    null)
                    ?? (arrayLength == -1
                        ? CreateArray(IntegerType, -1, 8)
                        : arrayLength is int fixedLength && fixedLength >= 0
                            ? CreateArray(IntegerType, fixedLength, checked(fixedLength * IntegerType.Size))
                            : type);
            }
            else if (typeForm == (int)OberonCompilerTypeForm.Pointer)
            {
                var importedPointerType = CreateImportedPointerType(
                    baseTypeForm,
                    baseTypeSize);
                if (importedPointerType is not null)
                {
                    type = importedPointerType;
                }
                else
                {
                    var importedBaseType = CreateImportedPrimitiveType(baseTypeForm);
                    if (importedBaseType is not null)
                    {
                        type = CreatePointer(importedBaseType);
                    }
                }
            }
            else
            {
                var importedPrimitiveType = CreateImportedPrimitiveType(typeForm);
                if (importedPrimitiveType is not null)
                {
                    type = importedPrimitiveType;
                }
            }
        }

        return new OberonCompilerObject($"arg{index}", OberonCompilerObjectClass.Parameter)
        {
            Type = type,
            ReadOnly = isReadOnly,
            OriginalName = procedureName
        };
    }

    public OberonCompilerScope OpenScope()
    {
        _currentScope = new OberonCompilerScope(_currentScope, _currentScope.Level + 1);
        return _currentScope;
    }

    public void CloseScope()
    {
        if (_currentScope.Parent is null)
        {
            throw new InvalidOperationException("cannot close the universe scope");
        }
        _currentScope = _currentScope.Parent;
    }

    public void AddDiagnostic(string message, string name) =>
        _diagnostics.Add(new OberonCompilerSymbolDiagnostic(message, name));

    private void EnterBuiltinType(string name, OberonCompilerType type) =>
        Declare(name, OberonCompilerObjectClass.Type, type);

    private void EnterSystemBuiltins()
    {
        EnterSystem("H", OberonCompilerObjectClass.Function, IntegerType, 201);
        EnterSystem("COND", OberonCompilerObjectClass.Function, BooleanType, 191);
        EnterSystem("SIZE", OberonCompilerObjectClass.Function, IntegerType, 181);
        EnterSystem("ADR", OberonCompilerObjectClass.Function, IntegerType, 171);
        EnterSystem("VAL", OberonCompilerObjectClass.Function, IntegerType, 162);
        EnterSystem("REG", OberonCompilerObjectClass.Function, IntegerType, 151);
        EnterSystem("BIT", OberonCompilerObjectClass.Function, BooleanType, 142);
        EnterSystem("LDREG", OberonCompilerObjectClass.Procedure, NoneType, 142);
        EnterSystem("LDPSR", OberonCompilerObjectClass.Procedure, NoneType, 131);
        EnterSystem("COPY", OberonCompilerObjectClass.Procedure, NoneType, 123);
        EnterSystem("PUT", OberonCompilerObjectClass.Procedure, NoneType, 112);
        EnterSystem("GET", OberonCompilerObjectClass.Procedure, NoneType, 102);
    }

    private void EnterSystem(string name, OberonCompilerObjectClass @class, OberonCompilerType type, int value)
    {
        var member = new OberonCompilerObject(name, @class) { Type = type, Value = value };
        SystemModule.AddMember(member);
    }
}
