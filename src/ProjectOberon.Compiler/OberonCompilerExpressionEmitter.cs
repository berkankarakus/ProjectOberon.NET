namespace ProjectOberon.Compiler;

public sealed class OberonCompilerExpressionEmitter
{
    public const int ModuleDataBase = OberonCompilerCallingConvention.ModuleDataBase;

    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly OberonCompilerRegisterAllocator _registers;
    private readonly Func<OberonCompilerCallExpression, int, int>? _emitFunctionCall;
    private readonly int _moduleDataOffset;
    private readonly Func<OberonCompilerType, int?>? _typeDescriptorResolver;

    public OberonCompilerExpressionEmitter(IOberonCompilerInstructionWriter generator)
        : this(
            generator,
            new OberonCompilerAddressEmitter(generator),
            new OberonCompilerRegisterAllocator(),
            emitFunctionCall: null,
            moduleDataOffset: 0,
            typeDescriptorResolver: null)
    {
    }

    internal OberonCompilerExpressionEmitter(
        IOberonCompilerInstructionWriter generator,
        OberonCompilerAddressEmitter addresses,
        OberonCompilerRegisterAllocator registers,
        Func<OberonCompilerCallExpression, int, int>? emitFunctionCall,
        int moduleDataOffset,
        Func<OberonCompilerType, int?>? typeDescriptorResolver)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(registers);

        _generator = generator;
        _addresses = addresses;
        _registers = registers;
        _emitFunctionCall = emitFunctionCall;
        _moduleDataOffset = moduleDataOffset;
        _typeDescriptorResolver = typeDescriptorResolver;
    }

    public int EmitExpression(OberonCompilerExpression expression, int destination = 0)
    {
        ArgumentNullException.ThrowIfNull(expression);
        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);

        switch (expression)
        {
            case OberonCompilerIntegerLiteralExpression literal:
                _generator.EmitMoveImmediate(destination, checked((int)literal.Value));
                return destination;
            case OberonCompilerRealLiteralExpression literal:
                _generator.EmitMoveImmediate(destination, BitConverter.SingleToInt32Bits((float)literal.Value));
                return destination;
            case OberonCompilerStringLiteralExpression literal:
                _addresses.EmitAddress(literal, destination);
                return destination;
            case OberonCompilerCallExpression call:
                if (_emitFunctionCall is null)
                    throw new InvalidOperationException("function call emission requires a statement emitter");
                return _emitFunctionCall(call, destination);
            case OberonCompilerFieldExpression field:
                return EmitField(field, destination);
            case OberonCompilerIndexExpression index:
                return EmitIndex(index, destination);
            case OberonCompilerDereferenceExpression dereference:
                return EmitDereference(dereference, destination);
            case OberonCompilerNameExpression name:
                return EmitName(name, destination);
            case OberonCompilerUnaryExpression unary:
                return EmitUnary(unary, destination);
            case OberonCompilerBinaryExpression binary:
                return EmitBinary(binary, destination);
            default:
                throw new NotSupportedException($"unsupported expression: {expression.GetType().Name}");
        }
    }

    private int EmitField(OberonCompilerFieldExpression field, int destination)
    {
        if (OberonCompilerTypeRules.IsAggregate(field.Field.Type))
        {
            _addresses.EmitAddress(field, destination);
            return destination;
        }

        return EmitScalarAddressLoad(
            field,
            field.Field.Type,
            "field expression cannot be loaded as a scalar",
            destination
        );
    }

    private int EmitIndex(OberonCompilerIndexExpression index, int destination)
    {
        if (OberonCompilerTypeRules.IsAggregate(index.ElementType))
        {
            _addresses.EmitAddress(index, destination);
            return destination;
        }

        return EmitScalarAddressLoad(
            index,
            index.ElementType,
            "array element expression cannot be loaded as a scalar",
            destination
        );
    }

    private int EmitDereference(OberonCompilerDereferenceExpression dereference, int destination)
    {
        if (OberonCompilerTypeRules.IsAggregate(dereference.ElementType))
        {
            _addresses.EmitAddress(dereference, destination);
            return destination;
        }

        return EmitScalarAddressLoad(
            dereference,
            dereference.ElementType,
            "pointer dereference expression cannot be loaded as a scalar",
            destination
        );
    }

    private int EmitScalarAddressLoad(
        OberonCompilerExpression expression,
        OberonCompilerType? type,
        string nonScalarError,
        int destination)
    {
        if (!OberonCompilerTypeRules.IsScalar(type))
        {
            throw new NotSupportedException(nonScalarError);
        }

        _addresses.EmitAddress(expression, destination);
        _generator.EmitLoad(
            destination,
            destination,
            0,
            OberonCompilerTypeRules.IsByteSized(type));
        return destination;
    }

    private int EmitName(OberonCompilerNameExpression name, int destination)
    {
        var symbol = name.Symbol ?? throw new InvalidOperationException(
            $"identifier '{name.Name}' is not defined");

        if (OberonCompilerTypeRules.IsAggregate(symbol.Type))
        {
            _addresses.EmitAddress(name, destination);
            return destination;
        }

        switch (symbol.Class)
        {
            case OberonCompilerObjectClass.Constant:
                _generator.EmitMoveImmediate(destination, checked((int)symbol.Value));
                return destination;
            case OberonCompilerObjectClass.Variable when symbol.Level == 0:
                _addresses.EmitAddress(name, destination);
                _generator.EmitLoad(
                    destination,
                    destination,
                    0,
                    OberonCompilerTypeRules.IsByteSized(symbol.Type));
                return destination;
            case OberonCompilerObjectClass.Variable when symbol.Level == 1:
                _generator.EmitLoad(
                    destination,
                    ModuleDataBase,
                    checked((int)symbol.Value + _moduleDataOffset),
                    OberonCompilerTypeRules.IsByteSized(symbol.Type));
                return destination;
            case OberonCompilerObjectClass.Variable:
            case OberonCompilerObjectClass.Parameter when symbol.ReadOnly:
                var valueBase = _addresses.EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, valueBase, checked((int)symbol.Value),
                    OberonCompilerTypeRules.IsByteSized(symbol.Type));
                return destination;
            case OberonCompilerObjectClass.Parameter:
                var referenceBase = _addresses.EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, referenceBase, checked((int)symbol.Value));
                _generator.EmitLoad(destination, destination, 0,
                    OberonCompilerTypeRules.IsByteSized(symbol.Type));
                return destination;
            default:
                throw new NotSupportedException($"symbol '{name.Name}' cannot be lowered yet");
        }
    }

    private int EmitUnary(OberonCompilerUnaryExpression unary, int destination)
    {
        if (unary.Operator == OberonCompilerTokenKind.Minus
            && !IsReal(unary.Operand)
            && TryEvaluateInteger(unary, out var constant))
        {
            _generator.EmitMoveImmediate(destination, constant);
            return destination;
        }

        EmitExpression(unary.Operand, destination);
        switch (unary.Operator)
        {
            case OberonCompilerTokenKind.Plus:
                return destination;
            case OberonCompilerTokenKind.Minus:
                var temporary = _registers.AcquireTemporary(destination);
                try
                {
                    var operation = IsReal(unary.Operand) ? AluOperation.Fsb : AluOperation.Sub;
                    _generator.EmitMoveImmediate(temporary,
                        IsReal(unary.Operand) ? BitConverter.SingleToInt32Bits(0f) : 0);
                    _generator.EmitRegister(operation, destination, temporary, destination);
                }
                finally
                {
                    _registers.ReleaseTemporary(temporary);
                }
                return destination;
            case OberonCompilerTokenKind.Not:
                _generator.EmitImmediate(AluOperation.Xor, destination, destination, 1);
                return destination;
            default:
                throw new NotSupportedException($"unary operator {unary.Operator} cannot be lowered yet");
        }
    }

    private int EmitBinary(OberonCompilerBinaryExpression binary, int destination)
    {
        if (binary.Operator == OberonCompilerTokenKind.Is)
        {
            return EmitTypeTest(binary, destination);
        }

        if (TryEmitIntegerConstant(binary, destination))
        {
            return destination;
        }

        if (TryEmitIntegerIdentity(binary, destination))
        {
            return destination;
        }

        if (TryEmitIntegerImmediate(binary, destination))
        {
            return destination;
        }

        if (TryEmitIntegerComparisonImmediate(binary, destination))
        {
            return destination;
        }

        var temporary = _registers.AcquireTemporary(destination);
        using var protectedDestination = _registers.ReserveRangeScope(destination, 1);
        try
        {
            EmitExpression(binary.Left, destination);
            EmitExpression(binary.Right, temporary);
            if (IsRelation(binary.Operator))
            {
                return EmitComparison(binary, destination, temporary);
            }
            if (binary.Operator == OberonCompilerTokenKind.Mod)
            {
                if (IsReal(binary))
                    throw new NotSupportedException("MOD requires integer operands");
                _generator.EmitRegister(AluOperation.Div, destination, destination, temporary);
                _generator.EmitMoveFromHigh(destination);
                return destination;
            }

            if (IsSet(binary))
            {
                var setOperation = binary.Operator switch
                {
                    OberonCompilerTokenKind.Plus => AluOperation.Ior,
                    OberonCompilerTokenKind.Minus => AluOperation.Ann,
                    OberonCompilerTokenKind.Multiply => AluOperation.And,
                    _ => throw new NotSupportedException(
                        $"operator {binary.Operator} is not valid for SET")
                };
                _generator.EmitRegister(setOperation, destination, destination, temporary);
                return destination;
            }

            var operation = IsReal(binary)
                ? binary.Operator switch
                {
                    OberonCompilerTokenKind.Plus => AluOperation.Fad,
                    OberonCompilerTokenKind.Minus => AluOperation.Fsb,
                    OberonCompilerTokenKind.Multiply => AluOperation.Fml,
                    OberonCompilerTokenKind.Divide or OberonCompilerTokenKind.RealDivide => AluOperation.Fdv,
                    _ => throw new NotSupportedException($"operator {binary.Operator} is not valid for REAL")
                }
                : binary.Operator switch
            {
                OberonCompilerTokenKind.Plus => AluOperation.Add,
                OberonCompilerTokenKind.Minus => AluOperation.Sub,
                OberonCompilerTokenKind.Multiply => AluOperation.Mul,
                OberonCompilerTokenKind.Divide => AluOperation.Div,
                OberonCompilerTokenKind.And => AluOperation.And,
                OberonCompilerTokenKind.Or => AluOperation.Ior,
                _ => throw new NotSupportedException($"binary operator {binary.Operator} cannot be lowered yet")
            };
            _generator.EmitRegister(operation, destination, destination, temporary);
            return destination;
        }
        finally
        {
            _registers.ReleaseTemporary(temporary);
        }
    }

    private bool TryEmitIntegerImmediate(
        OberonCompilerBinaryExpression binary,
        int destination)
    {
        if (binary.Right is not OberonCompilerIntegerLiteralExpression literal
            || IsReal(binary)
            || IsSet(binary)
            || IsRelation(binary.Operator)
            || literal.Value is < -0x8000 or > 0xffff)
        {
            return false;
        }

        AluOperation? operation = binary.Operator switch
        {
            OberonCompilerTokenKind.Plus => AluOperation.Add,
            OberonCompilerTokenKind.Minus => AluOperation.Sub,
            OberonCompilerTokenKind.Multiply => AluOperation.Mul,
            OberonCompilerTokenKind.Divide => AluOperation.Div,
            OberonCompilerTokenKind.And => AluOperation.And,
            OberonCompilerTokenKind.Or => AluOperation.Ior,
            _ => null
        };

        if (operation is not AluOperation immediateOperation)
        {
            return false;
        }

        EmitExpression(binary.Left, destination);
        _generator.EmitImmediate(
            immediateOperation,
            destination,
            destination,
            checked((int)literal.Value));
        return true;
    }

    private bool TryEmitIntegerIdentity(
        OberonCompilerBinaryExpression binary,
        int destination)
    {
        if (binary.Right is not OberonCompilerIntegerLiteralExpression literal
            || !IsInteger(binary.Left))
        {
            return false;
        }

        var isIdentity = binary.Operator switch
        {
            OberonCompilerTokenKind.Plus or OberonCompilerTokenKind.Minus => literal.Value == 0,
            OberonCompilerTokenKind.Multiply or OberonCompilerTokenKind.Divide => literal.Value == 1,
            _ => false
        };
        if (!isIdentity)
        {
            return false;
        }

        EmitExpression(binary.Left, destination);
        return true;
    }

    private bool TryEmitIntegerConstant(
        OberonCompilerBinaryExpression binary,
        int destination)
    {
        if (!TryEvaluateInteger(binary, out var value))
        {
            return false;
        }

        _generator.EmitMoveImmediate(destination, value);
        return true;
    }

    private static bool TryEvaluateInteger(
        OberonCompilerExpression expression,
        out int value)
    {
        if (expression is OberonCompilerIntegerLiteralExpression literal
            && IsInteger(literal)
            && literal.Value is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)literal.Value;
            return true;
        }

        if (expression is OberonCompilerUnaryExpression unary
            && TryEvaluateInteger(unary.Operand, out var operandValue))
        {
            switch (unary.Operator)
            {
                case OberonCompilerTokenKind.Plus:
                    value = operandValue;
                    return true;
                case OberonCompilerTokenKind.Minus:
                    var negated = -(long)operandValue;
                    if (negated is < int.MinValue or > int.MaxValue)
                    {
                        break;
                    }
                    value = (int)negated;
                    return true;
            }
        }

        if (expression is not OberonCompilerBinaryExpression binary
            || !TryEvaluateInteger(binary.Left, out var left)
            || !TryEvaluateInteger(binary.Right, out var right))
        {
            value = 0;
            return false;
        }

        long result;
        switch (binary.Operator)
        {
            case OberonCompilerTokenKind.Plus:
                result = (long)left + right;
                break;
            case OberonCompilerTokenKind.Minus:
                result = (long)left - right;
                break;
            case OberonCompilerTokenKind.Multiply:
                result = (long)left * right;
                break;
            case OberonCompilerTokenKind.Divide:
                if (right == 0)
                {
                    value = 0;
                    return false;
                }
                result = (long)left / right;
                break;
            case OberonCompilerTokenKind.Mod:
                if (right == 0)
                {
                    value = 0;
                    return false;
                }
                result = (long)left % right;
                break;
            case OberonCompilerTokenKind.And:
                result = left & right;
                break;
            case OberonCompilerTokenKind.Or:
                result = left | right;
                break;
            default:
                value = 0;
                return false;
        }

        if (result is < int.MinValue or > int.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (int)result;
        return true;
    }

    private bool TryEmitIntegerComparisonImmediate(
        OberonCompilerBinaryExpression binary,
        int destination)
    {
        if (binary.Operator is not (
                OberonCompilerTokenKind.Equal or
                OberonCompilerTokenKind.NotEqual or
                OberonCompilerTokenKind.Less or
                OberonCompilerTokenKind.LessOrEqual or
                OberonCompilerTokenKind.Greater or
                OberonCompilerTokenKind.GreaterOrEqual))
        {
            return false;
        }

        var comparison = binary.Operator;
        OberonCompilerIntegerLiteralExpression literal;
        if (binary.Right is OberonCompilerIntegerLiteralExpression rightLiteral
            && IsInteger(binary.Left)
            && IsInteger(rightLiteral))
        {
            literal = rightLiteral;
            comparison = binary.Operator;
        }
        else if (binary.Left is OberonCompilerIntegerLiteralExpression leftLiteral
            && IsInteger(binary.Right)
            && IsInteger(leftLiteral))
        {
            literal = leftLiteral;
            comparison = ReverseComparison(binary.Operator);
        }
        else
        {
            return false;
        }

        if (literal.Value is < -0x8000 or > 0xffff)
        {
            return false;
        }

        if (binary.Right is OberonCompilerIntegerLiteralExpression)
        {
            EmitExpression(binary.Left, destination);
        }
        else
        {
            EmitExpression(binary.Right, destination);
        }

        var (condition, invert) = ComparisonBranch(comparison);
        _generator.EmitImmediate(
            AluOperation.Sub,
            destination,
            destination,
            checked((int)literal.Value));
        EmitBooleanFromCondition(destination, condition, invert);
        return true;
    }

    private static OberonCompilerTokenKind ReverseComparison(
        OberonCompilerTokenKind operation) => operation switch
    {
        OberonCompilerTokenKind.Equal => OberonCompilerTokenKind.Equal,
        OberonCompilerTokenKind.NotEqual => OberonCompilerTokenKind.NotEqual,
        OberonCompilerTokenKind.Less => OberonCompilerTokenKind.Greater,
        OberonCompilerTokenKind.LessOrEqual => OberonCompilerTokenKind.GreaterOrEqual,
        OberonCompilerTokenKind.Greater => OberonCompilerTokenKind.Less,
        OberonCompilerTokenKind.GreaterOrEqual => OberonCompilerTokenKind.LessOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private int EmitComparison(
        OberonCompilerBinaryExpression comparison,
        int destination,
        int rightRegister)
    {
        if (comparison.Operator == OberonCompilerTokenKind.In)
        {
            _generator.EmitRegister(AluOperation.And, destination, destination, rightRegister);
            EmitBooleanFromNonZero(destination);
            return destination;
        }

        if (IsReal(comparison))
        {
            EmitRealComparison(comparison.Operator, destination, rightRegister);
            return destination;
        }

        var (condition, invert) = ComparisonBranch(comparison.Operator);
        _generator.EmitRegister(AluOperation.Sub, destination, destination, rightRegister);
        EmitBooleanFromCondition(destination, condition, invert);
        return destination;
    }

    private int EmitTypeTest(
        OberonCompilerBinaryExpression typeTest,
        int destination)
    {
        var testedType = OberonCompilerExpressionTypeResolver.Resolve(typeTest.Right);
        if (testedType is null)
        {
            throw new NotSupportedException("IS requires a local record type descriptor");
        }

        var leftType = OberonCompilerExpressionTypeResolver.Resolve(typeTest.Left);
        if (leftType?.Form == OberonCompilerTypeForm.Pointer
            && OberonCompilerTypeRules.IsBaseType(leftType.Base, testedType))
        {
            _generator.EmitMoveImmediate(destination, 1);
            return destination;
        }

        if (_typeDescriptorResolver is null)
        {
            throw new NotSupportedException("IS requires a local record type descriptor");
        }

        var descriptorOffset = _typeDescriptorResolver(testedType);
        if (descriptorOffset is not int offset)
        {
            throw new NotSupportedException("IS requires a local record type descriptor");
        }

        var temporary = _registers.AcquireTemporary(destination);
        using var protectedDestination = _registers.ReserveRangeScope(destination, 1);
        try
        {
            EmitExpression(typeTest.Left, destination);
            _generator.EmitImmediate(AluOperation.Sub, destination, destination, 0);
            var nonNilLabel = new OberonCompilerLabel();
            var endLabel = new OberonCompilerLabel();
            _generator.EmitBranch(nonNilLabel, ConditionCode.Eq, link: false, invert: true);
            _generator.EmitMoveImmediate(destination, 0);
            _generator.EmitBranch(endLabel);
            _generator.Bind(nonNilLabel);
            _generator.EmitImmediate(AluOperation.Add, destination, destination, -8);
            _generator.EmitLoad(destination, destination, 0);
            if (testedType.ParameterCount > 0)
            {
                _generator.EmitLoad(
                    destination,
                    destination,
                    checked(testedType.ParameterCount * sizeof(int)));
            }
            _generator.EmitImmediate(
                AluOperation.Add,
                temporary,
                ModuleDataBase,
                checked(offset));
            _generator.EmitRegister(AluOperation.Sub, destination, destination, temporary);
            EmitBooleanFromCondition(destination, ConditionCode.Eq);
            _generator.Bind(endLabel);
            return destination;
        }
        finally
        {
            _registers.ReleaseTemporary(temporary);
        }
    }

    private void EmitRealComparison(
        OberonCompilerTokenKind operation,
        int leftRegister,
        int rightRegister)
    {
        _generator.EmitRegister(AluOperation.Fsb, leftRegister, leftRegister, rightRegister);

        var trueLabel = new OberonCompilerLabel();
        var falseLabel = new OberonCompilerLabel();
        var endLabel = new OberonCompilerLabel();

        switch (operation)
        {
            case OberonCompilerTokenKind.Equal:
                _generator.EmitBranch(trueLabel, ConditionCode.Eq);
                break;
            case OberonCompilerTokenKind.NotEqual:
                _generator.EmitBranch(trueLabel, ConditionCode.Eq, link: false, invert: true);
                break;
            case OberonCompilerTokenKind.Less:
                _generator.EmitBranch(trueLabel, ConditionCode.Mi);
                break;
            case OberonCompilerTokenKind.LessOrEqual:
                _generator.EmitBranch(trueLabel, ConditionCode.Mi);
                _generator.EmitBranch(trueLabel, ConditionCode.Eq);
                break;
            case OberonCompilerTokenKind.Greater:
                _generator.EmitBranch(falseLabel, ConditionCode.Mi);
                _generator.EmitBranch(falseLabel, ConditionCode.Eq);
                _generator.EmitBranch(trueLabel);
                break;
            case OberonCompilerTokenKind.GreaterOrEqual:
                _generator.EmitBranch(falseLabel, ConditionCode.Mi);
                _generator.EmitBranch(trueLabel);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }

        _generator.EmitBranch(falseLabel);
        _generator.Bind(trueLabel);
        _generator.EmitMoveImmediate(leftRegister, 1);
        _generator.EmitBranch(endLabel);
        _generator.Bind(falseLabel);
        _generator.EmitMoveImmediate(leftRegister, 0);
        _generator.Bind(endLabel);
    }

    private void EmitBooleanFromNonZero(int register)
    {
        EmitBooleanFromCondition(register, ConditionCode.Eq, invert: true);
    }

    private void EmitBooleanFromCondition(
        int destination,
        ConditionCode condition,
        bool invert = false)
    {
        var trueLabel = new OberonCompilerLabel();
        var endLabel = new OberonCompilerLabel();
        _generator.EmitBranch(trueLabel, condition, link: false, invert: invert);
        _generator.EmitMoveImmediate(destination, 0);
        _generator.EmitBranch(endLabel);
        _generator.Bind(trueLabel);
        _generator.EmitMoveImmediate(destination, 1);
        _generator.Bind(endLabel);
    }

    private static (ConditionCode Condition, bool Invert) ComparisonBranch(
        OberonCompilerTokenKind operation) => operation switch
    {
        OberonCompilerTokenKind.Equal => (ConditionCode.Eq, false),
        OberonCompilerTokenKind.NotEqual => (ConditionCode.Eq, true),
        OberonCompilerTokenKind.Less => (ConditionCode.Lt, false),
        OberonCompilerTokenKind.LessOrEqual => (ConditionCode.Le, false),
        OberonCompilerTokenKind.Greater => (ConditionCode.Le, true),
        OberonCompilerTokenKind.GreaterOrEqual => (ConditionCode.Lt, true),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static bool IsRelation(OberonCompilerTokenKind operation) => operation is
        OberonCompilerTokenKind.Equal or OberonCompilerTokenKind.NotEqual or
        OberonCompilerTokenKind.Less or OberonCompilerTokenKind.LessOrEqual or
        OberonCompilerTokenKind.Greater or OberonCompilerTokenKind.GreaterOrEqual or
        OberonCompilerTokenKind.In;

    private static bool IsReal(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form == OberonCompilerTypeForm.Real;

    private static bool IsReal(OberonCompilerBinaryExpression expression) =>
        IsReal(expression.Left) || IsReal(expression.Right);

    private static bool IsInteger(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form is
            OberonCompilerTypeForm.Byte or
            OberonCompilerTypeForm.Character or
            OberonCompilerTypeForm.Integer;

    private static bool IsSet(OberonCompilerBinaryExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression.Left)?.Form == OberonCompilerTypeForm.Set;

}
