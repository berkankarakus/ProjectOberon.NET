namespace ProjectOberon.Emulator;

public sealed record OberonCompilerModuleParseResult(
    string? ModuleName,
    OberonCompilerObject? Module,
    OberonCompilerScope? ModuleScope,
    IReadOnlyList<OberonCompilerDiagnostic> Diagnostics,
    OberonCompilerStatementSequence? Body = null,
    IReadOnlyList<OberonCompilerProcedureDefinition>? Procedures = null)
{
    public bool Succeeded => Diagnostics.Count == 0 && Module is not null;
}

public sealed partial class OberonCompilerParser
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
        OberonCompilerStatementSequence? body = null;

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

        var procedures = ParseProcedureDeclarations();

        if (_current.Kind == OberonCompilerTokenKind.Begin)
        {
            Next();
            body = ParseStatementSequence();
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
        if (_diagnostics.Count == 0)
        {
            var semanticChecker = new OberonCompilerSemanticChecker(_symbols.BooleanType);
            _diagnostics.AddRange(semanticChecker.Check(body, procedures));
        }

        return new OberonCompilerModuleParseResult(
            moduleName,
            module,
            moduleScope,
            _diagnostics,
            body,
            procedures);
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

    private IReadOnlyList<OberonCompilerProcedureDefinition> ParseProcedureDeclarations()
    {
        var procedures = new List<OberonCompilerProcedureDefinition>();
        while (_current.Kind == OberonCompilerTokenKind.Procedure)
        {
            var procedure = ParseProcedureDeclaration();
            if (procedure is not null) procedures.Add(procedure);
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after procedure declaration");
        }

        return procedures;
    }

    private OberonCompilerProcedureDefinition? ParseProcedureDeclaration()
    {
        Next();
        var name = TryIdentifier(out var procedureName) ? procedureName : string.Empty;
        var exported = TakeExportMarker();
        if (name.Length == 0)
        {
            RecoverTo(OberonCompilerTokenKind.Semicolon,
                OberonCompilerTokenKind.End,
                OberonCompilerTokenKind.EndOfText);
            return null;
        }

        var procedureType = _symbols.CreateProcedure(_symbols.NoneType, []);
        var procedure = _symbols.Declare(
            name,
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: exported);

        _symbols.OpenScope();
        var savedSection = _declarationSection;
        var savedDataOffset = _dataOffset;
        try
        {
            _declarationSection = 0;
            var hasStaticLink = procedure.Level > 1;
            ParseFormalParameters(procedureType, hasStaticLink ? 8 : 4);
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after procedure header");
            _dataOffset = ParameterFrameSize(procedureType, hasStaticLink);
            ParseDeclarations();
            var frameSize = Align(_dataOffset, 4);
            var nestedProcedures = ParseProcedureDeclarations();
            OberonCompilerStatementSequence? body = null;

            if (_current.Kind == OberonCompilerTokenKind.Begin)
            {
                Next();
                body = ParseStatementSequence();
            }

            Expect(OberonCompilerTokenKind.End, "END expected after procedure");
            if (_current.Kind == OberonCompilerTokenKind.Identifier)
            {
                if (name.Length != 0 && _current.Lexeme != name)
                    AddDiagnostic($"procedure name '{name}' expected");
                Next();
            }

            return new OberonCompilerProcedureDefinition(
                procedure,
                frameSize,
                body,
                nestedProcedures);
        }
        finally
        {
            _symbols.CloseScope();
            _declarationSection = savedSection;
            _dataOffset = savedDataOffset;
        }
    }

    private void ParseFormalParameters(OberonCompilerType procedureType, int parameterOffset)
    {
        if (_current.Kind != OberonCompilerTokenKind.LeftParenthesis) return;
        Next();
        if (_current.Kind == OberonCompilerTokenKind.RightParenthesis)
        {
            Next();
            return;
        }

        var offset = parameterOffset;
        while (true)
        {
            var byReference = false;
            if (_current.Kind == OberonCompilerTokenKind.Var)
            {
                byReference = true;
                Next();
            }

            var names = ParseIdentifierList();
            Expect(OberonCompilerTokenKind.Colon, ": expected in parameter declaration");
            var type = ParseFormalType();
            foreach (var (name, _) in names)
            {
                offset = Align(offset, 4);
                var parameter = _symbols.Declare(
                    name,
                    OberonCompilerObjectClass.Parameter,
                    type,
                    offset,
                    readOnly: !byReference);
                procedureType.AddParameter(parameter);
                offset += ParameterSlotSize(type);
            }

            if (_current.Kind != OberonCompilerTokenKind.Semicolon) break;
            Next();
        }

        procedureType.ParameterCount = procedureType.Parameters.Count;
        Expect(OberonCompilerTokenKind.RightParenthesis, ") expected after parameter list");
        if (_current.Kind == OberonCompilerTokenKind.Colon)
        {
            Next();
            procedureType.Base = ParseType();
        }
    }

    private OberonCompilerType ParseFormalType()
    {
        if (_current.Kind != OberonCompilerTokenKind.Array) return ParseType();

        Next();
        Expect(OberonCompilerTokenKind.Of, "OF expected in open array parameter");
        var baseType = ParseFormalType();
        return _symbols.CreateArray(baseType, length: -1, size: 8);
    }

    private static int ParameterSlotSize(OberonCompilerType type) => type.Form switch
    {
        OberonCompilerTypeForm.Record => 4,
        OberonCompilerTypeForm.Array when type.Length < 0 => 8,
        OberonCompilerTypeForm.Array => 4,
        _ => 4
    };

    private static int ParameterFrameSize(OberonCompilerType procedureType, bool hasStaticLink) =>
        (hasStaticLink ? 8 : 4)
        + procedureType.Parameters.Sum(parameter => ParameterSlotSize(parameter.Type!));

    private void ParseConstants()
    {
        Next();
        while (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            var name = _current.Lexeme;
            Next();
            var exported = TakeExportMarker();
            Expect(OberonCompilerTokenKind.Equal, "= expected in constant declaration");
            var value = ParseConstantExpression();
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
