namespace ProjectOberon.Emulator;

public sealed record OberonCompilerModuleParseResult(
    string? ModuleName,
    OberonCompilerObject? Module,
    OberonCompilerScope? ModuleScope,
    IReadOnlyList<OberonCompilerDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0 && Module is not null;
}

public sealed class OberonCompilerParser
{
    private readonly OberonCompilerScanner _scanner;
    private readonly OberonCompilerSymbols _symbols;
    private readonly List<OberonCompilerDiagnostic> _diagnostics = [];
    private OberonCompilerToken _current;
    private int _declarationSection;
    private int _dataOffset;

    public OberonCompilerParser(string source, OberonCompilerSymbols? symbols = null)
    {
        _scanner = new OberonCompilerScanner(source);
        _symbols = symbols ?? new OberonCompilerSymbols();
        _current = _scanner.Scan();
    }

    public OberonCompilerModuleParseResult ParseModule()
    {
        string? moduleName = null;
        OberonCompilerObject? module = null;
        OberonCompilerScope? moduleScope = null;

        Expect(OberonCompilerTokenKind.Module, "MODULE expected");
        if (TryIdentifier(out var name))
        {
            moduleName = name;
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after module name");
            module = _symbols.OpenModule(name, name, 0);
            moduleScope = _symbols.OpenScope();
        }
        else
        {
            RecoverTo(OberonCompilerTokenKind.Semicolon, OberonCompilerTokenKind.EndOfText);
            if (_current.Kind == OberonCompilerTokenKind.Semicolon) Next();
        }

        ParseImports();
        ParseDeclarations();

        if (_current.Kind == OberonCompilerTokenKind.Procedure)
        {
            AddDiagnostic("procedure declarations are not implemented yet");
            RecoverTo(OberonCompilerTokenKind.Begin, OberonCompilerTokenKind.End,
                OberonCompilerTokenKind.EndOfText);
        }

        if (_current.Kind == OberonCompilerTokenKind.Begin)
        {
            Next();
            SkipStatementSequence();
        }

        Expect(OberonCompilerTokenKind.End, "END expected");
        if (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            if (moduleName is not null && _current.Lexeme != moduleName)
                AddDiagnostic($"module name '{moduleName}' expected");
            Next();
        }
        Expect(OberonCompilerTokenKind.Period, ". expected after module");

        foreach (var diagnostic in _scanner.Diagnostics) _diagnostics.Add(diagnostic);
        return new OberonCompilerModuleParseResult(moduleName, module, moduleScope, _diagnostics);
    }

    private void ParseImports()
    {
        if (_current.Kind != OberonCompilerTokenKind.Import) return;
        Next();
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var alias = _current.Lexeme;
            Next();
            var originalName = alias;
            if (_current.Kind == OberonCompilerTokenKind.Becomes)
            {
                Next();
                if (TryIdentifier(out var importedName)) originalName = importedName;
                else AddDiagnostic("module identifier expected after :=");
            }

            _symbols.OpenModule(alias, originalName, 0);
            if (_current.Kind != OberonCompilerTokenKind.Comma) break;
            Next();
        }
        Expect(OberonCompilerTokenKind.Semicolon, "; expected after IMPORT");
    }

    private void ParseDeclarations()
    {
        while (true)
        {
            switch (_current.Kind)
            {
                case OberonCompilerTokenKind.Const:
                    CheckDeclarationOrder(1);
                    ParseConstants();
                    break;
                case OberonCompilerTokenKind.Type:
                    CheckDeclarationOrder(2);
                    ParseTypes();
                    break;
                case OberonCompilerTokenKind.Var:
                    CheckDeclarationOrder(3);
                    ParseVariables();
                    break;
                default:
                    return;
            }
        }
    }

    private void ParseConstants()
    {
        Next();
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var name = _current.Lexeme;
            Next();
            var exported = TakeExportMarker();
            Expect(OberonCompilerTokenKind.Equal, "= expected in constant declaration");
            var value = ParseConstantValue();
            _symbols.Declare(name, OberonCompilerObjectClass.Constant, value.Type, value.Value, exported);
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after constant declaration");
        }
    }

    private void ParseTypes()
    {
        Next();
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var name = _current.Lexeme;
            Next();
            var exported = TakeExportMarker();
            Expect(OberonCompilerTokenKind.Equal, "= expected in type declaration");
            var type = ParseType();
            _symbols.Declare(name, OberonCompilerObjectClass.Type, type, exported: exported);
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after type declaration");
        }
    }

    private void ParseVariables()
    {
        Next();
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var names = ParseIdentifierList();
            Expect(OberonCompilerTokenKind.Colon, ": expected in variable declaration");
            var type = ParseType();
            foreach (var (name, exported) in names)
            {
                _dataOffset = Align(_dataOffset, type.Size);
                _symbols.Declare(name, OberonCompilerObjectClass.Variable, type, _dataOffset, exported);
                _dataOffset += type.Size;
            }
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after variable declaration");
        }
    }

    private OberonCompilerType ParseType()
    {
        if (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var name = _current.Lexeme;
            Next();
            var obj = _symbols.Lookup(name);
            if (obj?.Class == OberonCompilerObjectClass.Type && obj.Type is not null) return obj.Type;
            AddDiagnostic($"type '{name}' is not defined");
            return _symbols.NoneType;
        }

        if (_current.Kind == OberonCompilerTokenKind.Array)
        {
            Next();
            var length = ReadInteger("array length expected");
            Expect(OberonCompilerTokenKind.Of, "OF expected in array type");
            var baseType = ParseType();
            return _symbols.CreateArray(baseType, length, checked(baseType.Size * length));
        }

        if (_current.Kind == OberonCompilerTokenKind.Pointer)
        {
            Next();
            Expect(OberonCompilerTokenKind.To, "TO expected in pointer type");
            var baseType = ParseType();
            if (baseType.Form != OberonCompilerTypeForm.Record && baseType.Form != OberonCompilerTypeForm.None)
                AddDiagnostic("pointer base must be a record type");
            return _symbols.CreatePointer(baseType);
        }

        if (_current.Kind == OberonCompilerTokenKind.Record)
            return ParseRecordType();

        AddDiagnostic("type expected");
        return _symbols.NoneType;
    }

    private OberonCompilerType ParseRecordType()
    {
        Next();
        OberonCompilerType? baseType = null;
        if (_current.Kind == OberonCompilerTokenKind.LeftParenthesis)
        {
            Next();
            baseType = ParseType();
            Expect(OberonCompilerTokenKind.RightParenthesis, ") expected after record base type");
            if (baseType.Form != OberonCompilerTypeForm.Record)
            {
                AddDiagnostic("record base must be a record type");
                baseType = null;
            }
        }

        var record = _symbols.CreateRecord(baseType, (baseType?.ParameterCount ?? -1) + 1);
        var offset = baseType?.Size ?? 0;
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var fields = ParseIdentifierList();
            Expect(OberonCompilerTokenKind.Colon, ": expected in record field declaration");
            var fieldType = ParseType();
            foreach (var (name, exported) in fields)
            {
                offset = Align(offset, fieldType.Size);
                _symbols.DeclareField(record, name, fieldType, offset, exported);
                offset += fieldType.Size;
            }
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after record field declaration");
        }

        Expect(OberonCompilerTokenKind.End, "END expected after record type");
        record.Size = Align(offset, 4);
        return record;
    }

    private (OberonCompilerType Type, long Value) ParseConstantValue()
    {
        var negative = false;
        if (_current.Kind == OberonCompilerTokenKind.Minus)
        {
            negative = true;
            Next();
        }

        var token = _current;
        Next();
        var integerValue = token.IntegerValue ?? 0;
        return token.Kind switch
        {
            OberonCompilerTokenKind.Integer => (_symbols.IntegerType,
                negative ? -integerValue : integerValue),
            OberonCompilerTokenKind.Character => (_symbols.CharacterType, token.CharacterValue!.Value),
            OberonCompilerTokenKind.Real => (_symbols.RealType,
                BitConverter.SingleToInt32Bits((float)(negative ? -token.RealValue!.Value : token.RealValue!.Value))),
            OberonCompilerTokenKind.String => (_symbols.StringType, 0),
            OberonCompilerTokenKind.True => (_symbols.BooleanType, 1),
            OberonCompilerTokenKind.False => (_symbols.BooleanType, 0),
            OberonCompilerTokenKind.Nil => (_symbols.NilType, 0),
            OberonCompilerTokenKind.Identifier => ResolveConstant(token),
            _ => InvalidConstant()
        };
    }

    private (OberonCompilerType Type, long Value) ResolveConstant(OberonCompilerToken token)
    {
        var obj = _symbols.Lookup(token.Lexeme);
        if (obj?.Class == OberonCompilerObjectClass.Constant && obj.Type is not null) return (obj.Type, obj.Value);
        AddDiagnostic($"constant '{token.Lexeme}' is not defined");
        return (_symbols.IntegerType, 0);
    }

    private (OberonCompilerType Type, long Value) InvalidConstant()
    {
        AddDiagnostic("constant value expected");
        return (_symbols.IntegerType, 0);
    }

    private List<(string Name, bool Exported)> ParseIdentifierList()
    {
        var names = new List<(string Name, bool Exported)>();
        while (true)
        {
            if (!TryIdentifier(out var name)) break;
            names.Add((name, TakeExportMarker()));
            if (_current.Kind != OberonCompilerTokenKind.Comma) break;
            Next();
        }
        return names;
    }

    private void SkipStatementSequence()
    {
        var depth = 0;
        while (_current.Kind != OberonCompilerTokenKind.EndOfText)
        {
            if (_current.Kind is OberonCompilerTokenKind.If
                or OberonCompilerTokenKind.Case
                or OberonCompilerTokenKind.While
                or OberonCompilerTokenKind.Repeat
                or OberonCompilerTokenKind.Begin)
            {
                depth++;
            }
            else if (_current.Kind == OberonCompilerTokenKind.End)
            {
                if (depth == 0) return;
                depth--;
            }
            Next();
        }
    }

    private void CheckDeclarationOrder(int section)
    {
        if (section < _declarationSection) AddDiagnostic("declaration in bad order");
        _declarationSection = Math.Max(_declarationSection, section);
    }

    private bool TakeExportMarker()
    {
        if (_current.Kind != OberonCompilerTokenKind.Multiply) return false;
        Next();
        return true;
    }

    private int ReadInteger(string message)
    {
        if (_current.Kind == OberonCompilerTokenKind.Integer)
        {
            var value = _current.IntegerValue!.Value;
            Next();
            return value;
        }

        AddDiagnostic(message);
        return 0;
    }

    private bool TryIdentifier(out string name)
    {
        if (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            name = _current.Lexeme;
            Next();
            return true;
        }

        AddDiagnostic("identifier expected");
        name = string.Empty;
        return false;
    }

    private void Expect(OberonCompilerTokenKind kind, string message)
    {
        if (_current.Kind == kind) Next();
        else AddDiagnostic(message);
    }

    private void RecoverTo(params OberonCompilerTokenKind[] kinds)
    {
        while (!kinds.Contains(_current.Kind)) Next();
    }

    private void AddDiagnostic(string message) =>
        _diagnostics.Add(new OberonCompilerDiagnostic(message, _current.Position, _current.Line, _current.Column));

    private void Next() => _current = _scanner.Scan();

    private static int Align(int offset, int size) => size > 1 ? (offset + 3) / 4 * 4 : offset;
}
