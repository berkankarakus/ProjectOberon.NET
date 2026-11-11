namespace ProjectOberon.Compiler;

public sealed class OberonCompilerExpressionEmitter
{
    public const int ModuleDataBase = OberonCompilerCallingConvention.ModuleDataBase;

    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly OberonCompilerRegisterAllocator _registers;
    private readonly Func<OberonCompilerCallExpression, int, int>? _emitFunctionCall;

    public OberonCompilerExpressionEmitter(IOberonCompilerInstructionWriter generator)
        : this(
            generator,
            new OberonCompilerAddressEmitter(generator),
            new OberonCompilerRegisterAllocator(),
            emitFunctionCall: null)
    {
    }

    internal OberonCompilerExpressionEmitter(
        IOberonCompilerInstructionWriter generator,
        OberonCompilerAddressEmitter addresses,
        OberonCompilerRegisterAllocator registers,
        Func<OberonCompilerCallExpression, int, int>? emitFunctionCall)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(registers);

        _generator = generator;
        _addresses = addresses;
        _registers = registers;
        _emitFunctionCall = emitFunctionCall;
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
        if (!OberonCompilerTypeRules.IsScalar(field.Field.Type))
            throw new NotSupportedException("aggregate field loads are not implemented");
        _addresses.EmitAddress(field, destination);
        _generator.EmitLoad(destination, destination, 0, OberonCompilerTypeRules.IsByteSized(field.Field.Type));
        return destination;
    }

    private int EmitIndex(OberonCompilerIndexExpression index, int destination)
    {
        if (!OberonCompilerTypeRules.IsScalar(index.ElementType))
            throw new NotSupportedException("aggregate array element loads are not implemented");
        _addresses.EmitAddress(index, destination);
        _generator.EmitLoad(destination, destination, 0, OberonCompilerTypeRules.IsByteSized(index.ElementType));
        return destination;
    }

    private int EmitDereference(OberonCompilerDereferenceExpression dereference, int destination)
    {
        if (!OberonCompilerTypeRules.IsScalar(dereference.ElementType))
            throw new NotSupportedException("aggregate pointer dereference loads are not implemented");
        _addresses.EmitAddress(dereference, destination);
        _generator.EmitLoad(destination, destination, 0,
            OberonCompilerTypeRules.IsByteSized(dereference.ElementType));
        return destination;
    }

    private int EmitName(OberonCompilerNameExpression name, int destination)
    {
        var symbol = name.Symbol ?? throw new InvalidOperationException(
            $"identifier '{name.Name}' is not defined");
        switch (symbol.Class)
        {
            case OberonCompilerObjectClass.Constant:
                _generator.EmitMoveImmediate(destination, checked((int)symbol.Value));
                return destination;
            case OberonCompilerObjectClass.Variable when symbol.Level == 1:
                _generator.EmitLoad(destination, ModuleDataBase, checked((int)symbol.Value),
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
        var temporary = _registers.AcquireTemporary(destination);
        using var protectedDestination = _registers.ReserveRange(destination, 1);
        try
        {
            EmitExpression(binary.Left, destination);
            EmitExpression(binary.Right, temporary);
            if (IsRelation(binary.Operator)) return EmitComparison(binary, destination, temporary);
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

    private static bool IsReal(OberonCompilerExpression expression) => ExpressionType(expression)?.Form == OberonCompilerTypeForm.Real;

    private static bool IsReal(OberonCompilerBinaryExpression expression) =>
        IsReal(expression.Left) || IsReal(expression.Right);

    private static bool IsSet(OberonCompilerBinaryExpression expression) =>
        ExpressionType(expression.Left)?.Form == OberonCompilerTypeForm.Set;

    private static OberonCompilerType? ExpressionType(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerLiteralExpression literal => literal.Type,
        OberonCompilerNameExpression name => name.Symbol?.Type,
        OberonCompilerCallExpression call => call.Procedure.Symbol?.Type?.Base,
        OberonCompilerFieldExpression field => field.Field.Type,
        OberonCompilerIndexExpression index => index.ElementType,
        OberonCompilerDereferenceExpression dereference => dereference.ElementType,
        OberonCompilerUnaryExpression unary => ExpressionType(unary.Operand),
        OberonCompilerBinaryExpression binary => ExpressionType(binary.Left),
        _ => null
    };

}
