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

    public void EmitStatementSequence(OberonCompilerStatementSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        foreach (var statement in sequence.Statements) EmitStatement(statement);
    }

    private void EmitStatement(OberonCompilerStatement statement)
    {
        switch (statement)
        {
            case OberonCompilerAssignmentStatement assignment:
                EmitAssignment(assignment);
                break;
            case OberonCompilerReturnStatement { Value: var value }:
                if (value is not null) EmitExpression(value);
                _generator.EmitReturn();
                break;
            case OberonCompilerIfStatement conditional:
                EmitIf(conditional);
                break;
            case OberonCompilerWhileStatement loop:
                EmitWhile(loop);
                break;
            case OberonCompilerRepeatStatement repeat:
                EmitRepeat(repeat);
                break;
            case OberonCompilerCallStatement:
                throw new NotSupportedException("procedure call lowering is not implemented");
            default:
                throw new NotSupportedException($"unsupported statement: {statement.GetType().Name}");
        }
    }

    private void EmitIf(OberonCompilerIfStatement conditional)
    {
        var end = new OberonCompilerLabel();
        foreach (var branch in conditional.Branches)
        {
            var next = new OberonCompilerLabel();
            EmitBranchIfFalse(branch.Condition, next);
            EmitStatementSequence(branch.Body);
            _generator.EmitBranch(end);
            _generator.Bind(next);
        }

        if (conditional.ElseBody is not null) EmitStatementSequence(conditional.ElseBody);
        _generator.Bind(end);
    }

    private void EmitWhile(OberonCompilerWhileStatement loop)
    {
        var start = new OberonCompilerLabel();
        var end = new OberonCompilerLabel();
        _generator.Bind(start);
        EmitBranchIfFalse(loop.Condition, end);
        EmitStatementSequence(loop.Body);
        _generator.EmitBranch(start);
        _generator.Bind(end);
    }

    private void EmitRepeat(OberonCompilerRepeatStatement repeat)
    {
        var start = new OberonCompilerLabel();
        _generator.Bind(start);
        EmitStatementSequence(repeat.Body);
        EmitBranchIfFalse(repeat.Condition, start);
    }

    private void EmitBranchIfFalse(OberonCompilerExpression condition, OberonCompilerLabel falseLabel)
    {
        EmitExpression(condition);
        _generator.EmitMoveImmediate(1, 0);
        _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        _generator.EmitBranch(falseLabel, ConditionCode.Eq);
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
        if (IsRelation(binary.Operator)) return EmitComparison(binary, destination);

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
        _generator.EmitRegister(operation, destination, destination, destination + 1);
        return destination;
    }

    private int EmitComparison(OberonCompilerBinaryExpression comparison, int destination)
    {
        var (condition, invert) = ComparisonBranch(comparison.Operator);
        _generator.EmitRegister(AluOperation.Sub, destination, destination, destination + 1);
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

    private static void ValidateScratchRegister(int destination)
    {
        if (destination is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(destination),
                "expression registers must not overlap module base, stack pointer, or link register");
    }
}
