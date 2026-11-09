namespace ProjectOberon.Emulator;

public sealed class OberonCompilerExpressionEmitter
{
    public const int ModuleDataBase = 13;

    private readonly OberonCompilerCodeGenerator _generator;

    public OberonCompilerExpressionEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    public int EmitExpression(OberonCompilerExpression expression, int destination = 0)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ValidateScratchRegister(destination);

        switch (expression)
        {
            case OberonCompilerIntegerLiteralExpression literal:
                _generator.EmitMoveImmediate(destination, checked((int)literal.Value));
                return destination;
            case OberonCompilerRealLiteralExpression:
            case OberonCompilerStringLiteralExpression:
                throw new NotSupportedException("real and string lowering is not implemented");
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

    public void EmitAssignment(OberonCompilerAssignmentStatement assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.Target.Symbol?.Class != OberonCompilerObjectClass.Variable)
            throw new InvalidOperationException($"'{assignment.Target.Name}' is not a variable");
        if (assignment.Target.Symbol.Level != 1)
            throw new NotSupportedException("local and parameter stores are not implemented");

        EmitExpression(assignment.Value);
        _generator.EmitStore(0, ModuleDataBase, checked((int)assignment.Target.Symbol.Value));
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
                ValidateScratchRegister(destination + 1);
                _generator.EmitMoveImmediate(destination + 1, 0);
                _generator.EmitRegister(AluOperation.Sub, destination, destination + 1, destination);
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
        ValidateScratchRegister(destination + 1);
        EmitExpression(binary.Left, destination);
        EmitExpression(binary.Right, destination + 1);
        var operation = binary.Operator switch
        {
            OberonCompilerTokenKind.Plus => AluOperation.Add,
            OberonCompilerTokenKind.Minus => AluOperation.Sub,
            OberonCompilerTokenKind.Multiply => AluOperation.Mul,
            OberonCompilerTokenKind.Divide => AluOperation.Div,
            OberonCompilerTokenKind.And => AluOperation.And,
            OberonCompilerTokenKind.Or => AluOperation.Ior,
            _ => throw new NotSupportedException(
                $"binary operator {binary.Operator} cannot be lowered yet")
        };
        _generator.EmitRegister(operation, destination, destination, destination + 1);
        return destination;
    }

    private static void ValidateScratchRegister(int destination)
    {
        if (destination is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(destination),
                "expression registers must not overlap module base, stack pointer, or link register");
    }
}
