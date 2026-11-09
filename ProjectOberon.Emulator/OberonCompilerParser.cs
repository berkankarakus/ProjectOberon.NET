namespace ProjectOberon.Emulator;

public sealed record OberonCompilerModuleParseResult(
    string? ModuleName,
    OberonCompilerObject? Module,
    OberonCompilerScope? ModuleScope,
    IReadOnlyList<OberonCompilerDiagnostic> Diagnostics,
    OberonCompilerStatementSequence? Body = null)
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

        ParseProcedureDeclarations();

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
        return new OberonCompilerModuleParseResult(moduleName, module, moduleScope, _diagnostics, body);
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

    private void ParseProcedureDeclarations()
    {
        while (_current.Kind == OberonCompilerTokenKind.Procedure)
        {
            ParseProcedureDeclaration();
            Expect(OberonCompilerTokenKind.Semicolon, "; expected after procedure declaration");
        }
    }

    private void ParseProcedureDeclaration()
    {
        Next();
        var name = TryIdentifier(out var procedureName) ? procedureName : string.Empty;
        var exported = TakeExportMarker();
        if (name.Length == 0)
        {
            RecoverTo(OberonCompilerTokenKind.Semicolon,
                OberonCompilerTokenKind.End,
                OberonCompilerTokenKind.EndOfText);
            return;
        }

        var procedureType = _symbols.CreateProcedure(_symbols.NoneType, []);
        _symbols.Declare(
            name,
            OberonCompilerObjectClass.Procedure,
            procedureType,
            exported: exported);

        _symbols.OpenScope();
        ParseFormalParameters(procedureType);
        Expect(OberonCompilerTokenKind.Semicolon, "; expected after procedure header");

        var savedSection = _declarationSection;
        var savedDataOffset = _dataOffset;
        _declarationSection = 0;
        _dataOffset = 0;
        ParseDeclarations();
        ParseProcedureDeclarations();

        if (_current.Kind == OberonCompilerTokenKind.Begin)
        {
            Next();
            SkipStatementSequence();
        }

        Expect(OberonCompilerTokenKind.End, "END expected after procedure");
        if (_current.Kind == OberonCompilerTokenKind.Identifier)
        {
            if (name.Length != 0 && _current.Lexeme != name)
                AddDiagnostic($"procedure name '{name}' expected");
            Next();
        }

        _symbols.CloseScope();
        _declarationSection = savedSection;
        _dataOffset = savedDataOffset;
    }

    private void ParseFormalParameters(OberonCompilerType procedureType)
    {
        if (_current.Kind != OberonCompilerTokenKind.LeftParenthesis) return;
        Next();
        if (_current.Kind == OberonCompilerTokenKind.RightParenthesis)
        {
            Next();
            return;
        }

        var offset = 4;
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
            var type = ParseType();
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
                offset += 4;
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

    private ConstantValue ParseConstantExpression() =>
        ParseExpression(ParseConstantPrimary, ApplyConstantUnary, ApplyBinary);

    private OberonCompilerStatementSequence ParseStatementSequence()
    {
        var statements = new List<OberonCompilerStatement>();
        while (_current.Kind is not (OberonCompilerTokenKind.End
            or OberonCompilerTokenKind.Else
            or OberonCompilerTokenKind.Elsif
            or OberonCompilerTokenKind.Until
            or OberonCompilerTokenKind.EndOfText))
        {
            if (_current.Kind == OberonCompilerTokenKind.Semicolon)
            {
                Next();
                continue;
            }

            var statement = ParseStatement();
            if (statement is not null) statements.Add(statement);
            if (_current.Kind == OberonCompilerTokenKind.Semicolon)
            {
                Next();
            }
            else if (_current.Kind is not (OberonCompilerTokenKind.End
                or OberonCompilerTokenKind.Else
                or OberonCompilerTokenKind.Elsif
                or OberonCompilerTokenKind.Until
                or OberonCompilerTokenKind.EndOfText))
            {
                AddDiagnostic("; expected between statements");
                RecoverTo(OberonCompilerTokenKind.Semicolon,
                    OberonCompilerTokenKind.End,
                    OberonCompilerTokenKind.EndOfText);
                if (_current.Kind == OberonCompilerTokenKind.Semicolon) Next();
            }
        }

        return new OberonCompilerStatementSequence(statements);
    }

    private OberonCompilerStatement? ParseStatement()
    {
        if (_current.Kind == OberonCompilerTokenKind.Return)
        {
            Next();
            var value = _current.Kind == OberonCompilerTokenKind.Semicolon
                ? null
                : ParseRuntimeExpression();
            return new OberonCompilerReturnStatement(value);
        }

        if (_current.Kind != OberonCompilerTokenKind.Identifier)
        {
            AddDiagnostic("statement expected");
            Next();
            return null;
        }

        var name = _current.Lexeme;
        var symbol = _symbols.Lookup(name);
        Next();
        var reference = new OberonCompilerNameExpression(name, symbol);
        if (_current.Kind == OberonCompilerTokenKind.Becomes)
        {
            Next();
            return new OberonCompilerAssignmentStatement(reference, ParseRuntimeExpression());
        }

        var arguments = _current.Kind == OberonCompilerTokenKind.LeftParenthesis
            ? ParseArgumentList()
            : [];
        if (symbol?.Class != OberonCompilerObjectClass.Procedure)
            AddDiagnostic($"procedure '{name}' is not defined");
        return new OberonCompilerCallStatement(reference, arguments);
    }

    private IReadOnlyList<OberonCompilerExpression> ParseArgumentList()
    {
        Next();
        var arguments = new List<OberonCompilerExpression>();
        while (_current.Kind != OberonCompilerTokenKind.RightParenthesis
            && _current.Kind != OberonCompilerTokenKind.EndOfText)
        {
            arguments.Add(ParseRuntimeExpression());
            if (_current.Kind != OberonCompilerTokenKind.Comma) break;
            Next();
        }

        Expect(OberonCompilerTokenKind.RightParenthesis, ") expected after argument list");
        return arguments;
    }

    private OberonCompilerExpression ParseRuntimeExpression() =>
        ParseExpression(
            ParseRuntimePrimary,
            static (operation, value) => new OberonCompilerUnaryExpression(operation, value),
            static (operation, left, right) => new OberonCompilerBinaryExpression(operation, left, right));

    private T ParseExpression<T>(
        Func<T> parsePrimary,
        Func<OberonCompilerTokenKind, T, T> applyUnary,
        Func<OberonCompilerTokenKind, T, T, T> applyBinary)
    {
        var left = ParseSimpleExpression(parsePrimary, applyUnary, applyBinary);
        if (!IsRelation(_current.Kind)) return left;

        var operation = _current.Kind;
        Next();
        var right = ParseSimpleExpression(parsePrimary, applyUnary, applyBinary);
        return applyBinary(operation, left, right);
    }

    private T ParseSimpleExpression<T>(
        Func<T> parsePrimary,
        Func<OberonCompilerTokenKind, T, T> applyUnary,
        Func<OberonCompilerTokenKind, T, T, T> applyBinary)
    {
        var unary = _current.Kind is OberonCompilerTokenKind.Plus or OberonCompilerTokenKind.Minus
            ? _current.Kind
            : (OberonCompilerTokenKind?)null;
        if (unary is not null) Next();

        var expression = ParseTerm(parsePrimary, applyUnary, applyBinary);
        if (unary is not null) expression = applyUnary(unary.Value, expression);

        while (IsSimpleOperator(_current.Kind))
        {
            var operation = _current.Kind;
            Next();
            expression = applyBinary(operation, expression,
                ParseTerm(parsePrimary, applyUnary, applyBinary));
        }

        return expression;
    }

    private T ParseTerm<T>(
        Func<T> parsePrimary,
        Func<OberonCompilerTokenKind, T, T> applyUnary,
        Func<OberonCompilerTokenKind, T, T, T> applyBinary)
    {
        var expression = ParseFactor(parsePrimary, applyUnary, applyBinary);
        while (IsTermOperator(_current.Kind))
        {
            var operation = _current.Kind;
            Next();
            expression = applyBinary(operation, expression,
                ParseFactor(parsePrimary, applyUnary, applyBinary));
        }

        return expression;
    }

    private T ParseFactor<T>(
        Func<T> parsePrimary,
        Func<OberonCompilerTokenKind, T, T> applyUnary,
        Func<OberonCompilerTokenKind, T, T, T> applyBinary)
    {
        if (_current.Kind == OberonCompilerTokenKind.Not)
        {
            Next();
            return applyUnary(OberonCompilerTokenKind.Not,
                ParseFactor(parsePrimary, applyUnary, applyBinary));
        }

        if (_current.Kind == OberonCompilerTokenKind.LeftParenthesis)
        {
            Next();
            var expression = ParseExpression(parsePrimary, applyUnary, applyBinary);
            Expect(OberonCompilerTokenKind.RightParenthesis, ") expected in expression");
            return expression;
        }

        return parsePrimary();
    }

    private ConstantValue ParseConstantPrimary()
    {
        var token = _current;
        Next();
        return token.Kind switch
        {
            OberonCompilerTokenKind.Integer => ConstantValue.Integer(_symbols, token.IntegerValue ?? 0),
            OberonCompilerTokenKind.Character => ConstantValue.Character(_symbols, token.CharacterValue ?? 0),
            OberonCompilerTokenKind.Real => ConstantValue.Real(_symbols, token.RealValue ?? 0),
            OberonCompilerTokenKind.True => ConstantValue.Boolean(_symbols, true),
            OberonCompilerTokenKind.False => ConstantValue.Boolean(_symbols, false),
            OberonCompilerTokenKind.Nil => new ConstantValue(_symbols.NilType, 0, 0),
            OberonCompilerTokenKind.String => new ConstantValue(_symbols.StringType, 0, 0),
            OberonCompilerTokenKind.Identifier => ResolveConstantValue(token),
            _ => InvalidConstant()
        };
    }

    private OberonCompilerExpression ParseRuntimePrimary()
    {
        var token = _current;
        Next();
        return token.Kind switch
        {
            OberonCompilerTokenKind.Integer => new OberonCompilerLiteralExpression(
                _symbols.IntegerType, token.IntegerValue ?? 0),
            OberonCompilerTokenKind.Character => new OberonCompilerLiteralExpression(
                _symbols.CharacterType, token.CharacterValue ?? 0),
            OberonCompilerTokenKind.Real => new OberonCompilerLiteralExpression(
                _symbols.RealType, BitConverter.SingleToInt32Bits((float)(token.RealValue ?? 0))),
            OberonCompilerTokenKind.True => new OberonCompilerLiteralExpression(_symbols.BooleanType, 1),
            OberonCompilerTokenKind.False => new OberonCompilerLiteralExpression(_symbols.BooleanType, 0),
            OberonCompilerTokenKind.Nil => new OberonCompilerLiteralExpression(_symbols.NilType, 0),
            OberonCompilerTokenKind.Identifier => ResolveRuntimeName(token),
            _ => InvalidRuntimeExpression()
        };
    }

    private ConstantValue ApplyConstantUnary(OberonCompilerTokenKind operation, ConstantValue value)
    {
        if (operation == OberonCompilerTokenKind.Not)
        {
            if (value.Type.Form != OberonCompilerTypeForm.Boolean)
            {
                AddDiagnostic("boolean expression expected after ~");
                return ConstantValue.Boolean(_symbols, false);
            }

            return ConstantValue.Boolean(_symbols, value.IntegerValue == 0);
        }

        if (!value.IsNumeric) AddDiagnostic("numeric expression expected");
        return operation == OberonCompilerTokenKind.Minus ? value.Negate() : value;
    }

    private OberonCompilerExpression ResolveRuntimeName(OberonCompilerToken token)
    {
        var symbol = _symbols.Lookup(token.Lexeme);
        if (symbol is null) AddDiagnostic($"identifier '{token.Lexeme}' is not defined");
        return new OberonCompilerNameExpression(token.Lexeme, symbol);
    }

    private static bool IsSimpleOperator(OberonCompilerTokenKind kind) => kind is
        OberonCompilerTokenKind.Plus or OberonCompilerTokenKind.Minus or OberonCompilerTokenKind.Or;

    private static bool IsTermOperator(OberonCompilerTokenKind kind) => kind is
        OberonCompilerTokenKind.Multiply or OberonCompilerTokenKind.RealDivide or
        OberonCompilerTokenKind.Divide or OberonCompilerTokenKind.Mod or
        OberonCompilerTokenKind.And;

    private OberonCompilerExpression InvalidRuntimeExpression()
    {
        AddDiagnostic("expression expected");
        return new OberonCompilerLiteralExpression(_symbols.IntegerType, 0);
    }

    private ConstantValue ResolveConstantValue(OberonCompilerToken token)
    {
        var obj = _symbols.Lookup(token.Lexeme);
        if (obj?.Class != OberonCompilerObjectClass.Constant || obj.Type is null)
        {
            AddDiagnostic($"constant '{token.Lexeme}' is not defined");
            return ConstantValue.Integer(_symbols, 0);
        }

        if (obj.Type.Form == OberonCompilerTypeForm.Real)
            return ConstantValue.Real(_symbols, BitConverter.Int32BitsToSingle(checked((int)obj.Value)));
        return new ConstantValue(obj.Type, obj.Value, obj.Value);
    }

    private ConstantValue ApplyBinary(
        OberonCompilerTokenKind operation,
        ConstantValue left,
        ConstantValue right)
    {
        if (IsRelation(operation))
        {
            if (!AreComparable(left, right))
            {
                AddDiagnostic("incompatible types in comparison");
                return ConstantValue.Boolean(_symbols, false);
            }

            return ConstantValue.Boolean(_symbols, Compare(left, right, operation));
        }

        if (operation == OberonCompilerTokenKind.Or || operation == OberonCompilerTokenKind.And)
        {
            if (left.Type.Form != OberonCompilerTypeForm.Boolean || right.Type.Form != OberonCompilerTypeForm.Boolean)
            {
                AddDiagnostic("boolean operands expected");
                return ConstantValue.Boolean(_symbols, false);
            }

            var result = operation == OberonCompilerTokenKind.Or
                ? left.IntegerValue != 0 || right.IntegerValue != 0
                : left.IntegerValue != 0 && right.IntegerValue != 0;
            return ConstantValue.Boolean(_symbols, result);
        }

        if (!left.IsNumeric || !right.IsNumeric)
        {
            AddDiagnostic("numeric operands expected");
            return ConstantValue.Integer(_symbols, 0);
        }

        var real = left.Type.Form == OberonCompilerTypeForm.Real || right.Type.Form == OberonCompilerTypeForm.Real;
        if (operation == OberonCompilerTokenKind.Mod && real)
        {
            AddDiagnostic("MOD requires integer operands");
            return ConstantValue.Integer(_symbols, 0);
        }

        if (operation == OberonCompilerTokenKind.Divide && real)
        {
            AddDiagnostic("DIV requires integer operands");
            return ConstantValue.Integer(_symbols, 0);
        }

        if (operation == OberonCompilerTokenKind.RealDivide)
        {
            if (right.NumericValue == 0) AddDiagnostic("division by zero");
            return ConstantValue.Real(_symbols, left.NumericValue / right.NumericValue);
        }

        if (real)
        {
            var result = operation switch
            {
                OberonCompilerTokenKind.Plus => left.NumericValue + right.NumericValue,
                OberonCompilerTokenKind.Minus => left.NumericValue - right.NumericValue,
                OberonCompilerTokenKind.Multiply => left.NumericValue * right.NumericValue,
                _ => 0
            };
            return ConstantValue.Real(_symbols, result);
        }

        var integerResult = operation switch
        {
            OberonCompilerTokenKind.Plus => left.IntegerValue + right.IntegerValue,
            OberonCompilerTokenKind.Minus => left.IntegerValue - right.IntegerValue,
            OberonCompilerTokenKind.Multiply => left.IntegerValue * right.IntegerValue,
            OberonCompilerTokenKind.Divide => right.IntegerValue == 0 ? 0 : left.IntegerValue / right.IntegerValue,
            OberonCompilerTokenKind.Mod => right.IntegerValue == 0 ? 0 : left.IntegerValue % right.IntegerValue,
            _ => 0
        };
        if (operation is OberonCompilerTokenKind.Divide or OberonCompilerTokenKind.Mod && right.IntegerValue == 0)
            AddDiagnostic("division by zero");
        return ConstantValue.Integer(_symbols, integerResult);
    }

    private bool AreComparable(ConstantValue left, ConstantValue right) =>
        left.Type.Form == right.Type.Form || left.IsNumeric && right.IsNumeric;

    private bool Compare(ConstantValue left, ConstantValue right, OberonCompilerTokenKind operation)
    {
        var result = left.Type.Form == OberonCompilerTypeForm.Real || right.Type.Form == OberonCompilerTypeForm.Real
            ? (left.NumericValue, right.NumericValue)
            : (left.IntegerValue, right.IntegerValue);
        return operation switch
        {
            OberonCompilerTokenKind.Equal => result.Item1 == result.Item2,
            OberonCompilerTokenKind.NotEqual => result.Item1 != result.Item2,
            OberonCompilerTokenKind.Less => result.Item1 < result.Item2,
            OberonCompilerTokenKind.LessOrEqual => result.Item1 <= result.Item2,
            OberonCompilerTokenKind.Greater => result.Item1 > result.Item2,
            OberonCompilerTokenKind.GreaterOrEqual => result.Item1 >= result.Item2,
            _ => false
        };
    }

    private static bool IsRelation(OberonCompilerTokenKind kind) => kind is
        OberonCompilerTokenKind.Equal or OberonCompilerTokenKind.NotEqual or
        OberonCompilerTokenKind.Less or OberonCompilerTokenKind.LessOrEqual or
        OberonCompilerTokenKind.Greater or OberonCompilerTokenKind.GreaterOrEqual;

    private ConstantValue InvalidConstant()
    {
        AddDiagnostic("constant value expected");
        return ConstantValue.Integer(_symbols, 0);
    }

    private readonly record struct ConstantValue(
        OberonCompilerType Type,
        long IntegerValue,
        double NumericValue)
    {
        public long Value => IntegerValue;

        public bool IsNumeric => Type.Form is OberonCompilerTypeForm.Byte
            or OberonCompilerTypeForm.Character
            or OberonCompilerTypeForm.Integer
            or OberonCompilerTypeForm.Real;

        public ConstantValue Negate() => Type.Form == OberonCompilerTypeForm.Real
            ? this with
            {
                IntegerValue = BitConverter.SingleToInt32Bits((float)-NumericValue),
                NumericValue = -NumericValue
            }
            : this with { IntegerValue = -IntegerValue, NumericValue = -NumericValue };

        public static ConstantValue Integer(OberonCompilerSymbols symbols, long value) =>
            new(symbols.IntegerType, value, value);

        public static ConstantValue Character(OberonCompilerSymbols symbols, long value) =>
            new(symbols.CharacterType, value, value);

        public static ConstantValue Real(OberonCompilerSymbols symbols, double value) =>
            new(symbols.RealType, BitConverter.SingleToInt32Bits((float)value), value);

        public static ConstantValue Boolean(OberonCompilerSymbols symbols, bool value) =>
            new(symbols.BooleanType, value ? 1 : 0, value ? 1 : 0);
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
