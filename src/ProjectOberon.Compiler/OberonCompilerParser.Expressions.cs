namespace ProjectOberon.Compiler;

public sealed partial class OberonCompilerParser
{
    private ConstantValue ParseConstantExpression() =>
        ParseExpression(ParseConstantPrimary, ApplyConstantUnary, ApplyBinary);
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
            OberonCompilerTokenKind.Integer => new OberonCompilerIntegerLiteralExpression(
                _symbols.IntegerType, token.IntegerValue ?? 0),
            OberonCompilerTokenKind.Character => new OberonCompilerIntegerLiteralExpression(
                _symbols.CharacterType, token.CharacterValue ?? 0),
            OberonCompilerTokenKind.Real => new OberonCompilerRealLiteralExpression(
                _symbols.RealType, token.RealValue ?? 0),
            OberonCompilerTokenKind.True => new OberonCompilerIntegerLiteralExpression(_symbols.BooleanType, 1),
            OberonCompilerTokenKind.False => new OberonCompilerIntegerLiteralExpression(_symbols.BooleanType, 0),
            OberonCompilerTokenKind.Nil => new OberonCompilerIntegerLiteralExpression(_symbols.NilType, 0),
            OberonCompilerTokenKind.String => new OberonCompilerStringLiteralExpression(
                _symbols.StringType, token.StringValue ?? string.Empty),
            OberonCompilerTokenKind.LeftBrace => ParseRuntimeSetLiteral(),
            OberonCompilerTokenKind.Identifier => ParseRuntimeNameOrCall(token),
            _ => InvalidRuntimeExpression()
        };
    }

    private OberonCompilerExpression ParseRuntimeSetLiteral()
    {
        uint value = 0;
        while (_current.Kind is not (OberonCompilerTokenKind.RightBrace
            or OberonCompilerTokenKind.EndOfText))
        {
            var start = ParseConstantExpression();
            var end = start;
            if (_current.Kind == OberonCompilerTokenKind.Range)
            {
                Next();
                end = ParseConstantExpression();
            }

            if (!TryGetSetElement(start, out var first)
                || !TryGetSetElement(end, out var last))
            {
                AddDiagnostic("SET elements must be integer constants in the range 0..31");
            }
            else if (first > last)
            {
                AddDiagnostic("SET range must be ascending");
            }
            else
            {
                for (var element = first; element <= last; element++)
                    value |= 1u << element;
            }

            if (_current.Kind != OberonCompilerTokenKind.Comma) break;
            Next();
        }

        Expect(OberonCompilerTokenKind.RightBrace, "} expected after SET literal");
        return new OberonCompilerIntegerLiteralExpression(
            _symbols.SetType,
            unchecked((int)value));
    }

    private static bool TryGetSetElement(ConstantValue value, out int element)
    {
        if (value.Type.Form is OberonCompilerTypeForm.Byte
            or OberonCompilerTypeForm.Character
            or OberonCompilerTypeForm.Integer
            && value.IntegerValue is >= 0 and <= 31)
        {
            element = checked((int)value.IntegerValue);
            return true;
        }

        element = 0;
        return false;
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

    private OberonCompilerExpression ParseRuntimeNameOrCall(OberonCompilerToken token)
    {
        var reference = ResolveRuntimeName(token);
        OberonCompilerExpression expression = reference;
        if (_current.Kind == OberonCompilerTokenKind.LeftParenthesis)
            expression = new OberonCompilerCallExpression(reference, ParseArgumentList());
        return ParseSelectors(expression);
    }

    private OberonCompilerExpression ParseSelectors(OberonCompilerExpression expression)
    {
        while (_current.Kind is OberonCompilerTokenKind.Period
            or OberonCompilerTokenKind.LeftBracket
            or OberonCompilerTokenKind.Arrow)
        {
            if (_current.Kind == OberonCompilerTokenKind.Arrow)
            {
                Next();
                var pointerType = ExpressionType(expression);
                if (pointerType?.Form != OberonCompilerTypeForm.Pointer || pointerType.Base is null)
                {
                    AddDiagnostic("pointer dereference requires a POINTER receiver");
                    continue;
                }

                expression = new OberonCompilerDereferenceExpression(expression, pointerType.Base);
                continue;
            }

            if (_current.Kind == OberonCompilerTokenKind.LeftBracket)
            {
                Next();
                var index = ParseRuntimeExpression();
                Expect(OberonCompilerTokenKind.RightBracket, "] expected after array index");
                var arrayType = ExpressionType(expression);
                if (arrayType?.Form != OberonCompilerTypeForm.Array || arrayType.Base is null)
                {
                    AddDiagnostic("array index requires an ARRAY receiver");
                    continue;
                }

                expression = new OberonCompilerIndexExpression(expression, index, arrayType.Base);
                continue;
            }

            Next();
            if (_current.Kind != OberonCompilerTokenKind.Identifier)
            {
                AddDiagnostic("field identifier expected after .");
                return expression;
            }

            var fieldName = _current.Lexeme;
            Next();

            if (expression is OberonCompilerNameExpression moduleName
                && moduleName.Symbol?.Class == OberonCompilerObjectClass.Module)
            {
                var member = moduleName.Symbol.FindMember(fieldName);
                if (member is null)
                {
                    AddDiagnostic($"module member '{fieldName}' is not defined");
                    continue;
                }

                expression = new OberonCompilerNameExpression(
                    $"{moduleName.Name}.{fieldName}",
                    member);
                continue;
            }

            var receiverType = ExpressionType(expression);
            if (receiverType?.Form != OberonCompilerTypeForm.Record)
            {
                AddDiagnostic($"'{fieldName}' requires a record receiver");
                continue;
            }

            var field = _symbols.LookupField(receiverType, fieldName);
            if (field is null)
            {
                AddDiagnostic($"field '{fieldName}' is not defined");
                continue;
            }

            expression = new OberonCompilerFieldExpression(expression, field);
        }

        return expression;
    }

    private OberonCompilerType? ExpressionType(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerNameExpression name => name.Symbol?.Type,
        OberonCompilerCallExpression call => call.Procedure.Symbol?.Type?.Base,
        OberonCompilerFieldExpression field => field.Field.Type,
        OberonCompilerIndexExpression index => index.ElementType,
        OberonCompilerDereferenceExpression dereference => dereference.ElementType,
        _ => null
    };

    private void ValidateProcedureCall(OberonCompilerNameExpression procedure)
    {
        if (procedure.Symbol?.Class != OberonCompilerObjectClass.Procedure)
            AddDiagnostic($"procedure '{procedure.Name}' is not defined");
    }

    private OberonCompilerNameExpression ResolveRuntimeName(OberonCompilerToken token)
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
        return new OberonCompilerIntegerLiteralExpression(_symbols.IntegerType, 0);
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
                OberonCompilerTokenKind.RealDivide => right.NumericValue == 0 ? 0 : left.NumericValue / right.NumericValue,
                _ => 0
            };
            if (operation == OberonCompilerTokenKind.RealDivide && right.NumericValue == 0)
                AddDiagnostic("division by zero");
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
        OberonCompilerTokenKind.Greater or OberonCompilerTokenKind.GreaterOrEqual or
        OberonCompilerTokenKind.In or OberonCompilerTokenKind.Is;

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
}
