namespace ProjectOberon.Emulator;

public sealed class OberonCompilerExpressionEmitter
{
    public const int ModuleDataBase = OberonCompilerCallingConvention.ModuleDataBase;

    private readonly OberonCompilerCodeGenerator _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly OberonCompilerRegisterAllocator _registers;
    private readonly Func<OberonCompilerCallExpression, int, int>? _emitFunctionCall;

    public OberonCompilerExpressionEmitter(OberonCompilerCodeGenerator generator)
        : this(
            generator,
            new OberonCompilerAddressEmitter(generator),
            new OberonCompilerRegisterAllocator(),
            emitFunctionCall: null)
    {
    }

    internal OberonCompilerExpressionEmitter(
        OberonCompilerCodeGenerator generator,
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
            case OberonCompilerRealLiteralExpression:
            case OberonCompilerStringLiteralExpression:
                throw new NotSupportedException("real and string lowering is not implemented");
            case OberonCompilerCallExpression call:
                if (_emitFunctionCall is null)
                    throw new InvalidOperationException("function call emission requires a statement emitter");
                return _emitFunctionCall(call, destination);
            case OberonCompilerFieldExpression field:
                return EmitField(field, destination);
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
        if (!IsScalar(field.Field.Type))
            throw new NotSupportedException("aggregate field loads are not implemented");
        _addresses.EmitAddress(field, destination);
        _generator.EmitLoad(destination, destination, 0);
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
                _generator.EmitLoad(destination, ModuleDataBase, checked((int)symbol.Value));
                return destination;
            case OberonCompilerObjectClass.Variable:
            case OberonCompilerObjectClass.Parameter when symbol.ReadOnly:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer,
                    checked((int)symbol.Value));
                return destination;
            case OberonCompilerObjectClass.Parameter:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer,
                    checked((int)symbol.Value));
                _generator.EmitLoad(destination, destination, 0);
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
                    _generator.EmitMoveImmediate(temporary, 0);
                    _generator.EmitRegister(AluOperation.Sub, destination, temporary, destination);
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

            var operation = binary.Operator switch
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
        var (condition, invert) = ComparisonBranch(comparison.Operator);
        _generator.EmitRegister(AluOperation.Sub, destination, destination, rightRegister);
        var trueLabel = new OberonCompilerLabel();
        var end = new OberonCompilerLabel();
        _generator.EmitBranch(trueLabel, condition, link: false, invert: invert);
        _generator.EmitMoveImmediate(destination, 0);
        _generator.EmitBranch(end);
        _generator.Bind(trueLabel);
        _generator.EmitMoveImmediate(destination, 1);
        _generator.Bind(end);
        return destination;
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
        OberonCompilerTokenKind.Greater or OberonCompilerTokenKind.GreaterOrEqual;

    private static bool IsScalar(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean or
        OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer;
}
