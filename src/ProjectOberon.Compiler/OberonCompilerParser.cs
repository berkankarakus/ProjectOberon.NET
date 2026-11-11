namespace ProjectOberon.Compiler;

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
    private readonly IReadOnlyDictionary<string, OberonSymbolModule> _importedModules;
    private readonly List<OberonCompilerDiagnostic> _diagnostics = [];
    private OberonCompilerToken _current;
    private int _declarationSection;
    private int _dataOffset;

    public OberonCompilerParser(
        string source,
        OberonCompilerSymbols? symbols = null,
        IReadOnlyDictionary<string, OberonSymbolModule>? importedModules = null)
    {
        _scanner = new OberonCompilerScanner(source);
        _symbols = symbols ?? new OberonCompilerSymbols();
        _importedModules = importedModules ?? new Dictionary<string, OberonSymbolModule>(StringComparer.Ordinal);
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
            if (_current.Kind == OberonCompilerTokenKind.Semicolon)
            {
                Next();
            }
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

        foreach (var diagnostic in _scanner.Diagnostics)
        {
            _diagnostics.Add(diagnostic);
        }
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


    private List<(string Name, bool Exported)> ParseIdentifierList()
    {
        var names = new List<(string Name, bool Exported)>();
        while (true)
        {
            if (!TryIdentifier(out var name))
            {
                break;
            }
            names.Add((name, TakeExportMarker()));
            if (_current.Kind != OberonCompilerTokenKind.Comma)
            {
                break;
            }
            Next();
        }
        return names;
    }

    private void CheckDeclarationOrder(int section)
    {
        if (section < _declarationSection)
        {
            AddDiagnostic("declaration in bad order");
        }
        _declarationSection = Math.Max(_declarationSection, section);
    }

    private bool TakeExportMarker()
    {
        if (_current.Kind != OberonCompilerTokenKind.Multiply)
        {
            return false;
        }
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
        if (_current.Kind == kind)
        {
            Next();
        }
        else
        {
            AddDiagnostic(message);
        }
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
