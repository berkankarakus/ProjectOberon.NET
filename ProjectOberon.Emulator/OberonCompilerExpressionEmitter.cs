namespace ProjectOberon.Emulator;

public sealed class OberonCompilerExpressionEmitter
{
    public const int ModuleDataBase = 13;

    private readonly OberonCompilerCodeGenerator _generator;
    private readonly int _frameSize;

    public OberonCompilerExpressionEmitter(OberonCompilerCodeGenerator generator, int frameSize = 0)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _frameSize = frameSize;
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
            case OberonCompilerCallExpression call:
                return EmitFunctionCall(call, destination);
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
        var target = assignment.Target.Symbol;
        if (target?.Class is not (OberonCompilerObjectClass.Variable or OberonCompilerObjectClass.Parameter))
            throw new InvalidOperationException($"'{assignment.Target.Name}' is not a variable");

        EmitExpression(assignment.Value);
        var offset = checked((int)target.Value);
        if (target.Class == OberonCompilerObjectClass.Parameter && !target.ReadOnly)
        {
            _generator.EmitLoad(1, OberonCompilerCodeGenerator.StackPointer, offset);
            _generator.EmitStore(0, 1, 0);
            return;
        }

        var addressRegister = target.Level == 1 ? ModuleDataBase : OberonCompilerCodeGenerator.StackPointer;
        _generator.EmitStore(0, addressRegister, offset);
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
                EmitReturn();
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
                EmitCall((OberonCompilerCallStatement)statement);
                break;
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

    private void EmitCall(OberonCompilerCallStatement call)
    {
        var procedure = call.Procedure.Symbol ?? throw new InvalidOperationException(
            $"procedure '{call.Procedure.Name}' is not defined");
        EmitCallArguments(procedure, call.Arguments);
        var target = procedure.CodeLabel ?? throw new InvalidOperationException(
            $"procedure '{call.Procedure.Name}' has no code label");
        _generator.EmitCall(target);
    }

    private int EmitFunctionCall(OberonCompilerCallExpression call, int destination)
    {
        var procedure = call.Procedure.Symbol ?? throw new InvalidOperationException(
            $"function '{call.Procedure.Name}' is not defined");
        if (procedure.Type?.Base?.Form == OberonCompilerTypeForm.None)
            throw new InvalidOperationException($"'{call.Procedure.Name}' is a procedure, not a function");

        EmitCallArguments(procedure, call.Arguments);
        var target = procedure.CodeLabel ?? throw new InvalidOperationException(
            $"function '{call.Procedure.Name}' has no code label");
        _generator.EmitCall(target);
        if (destination != 0) _generator.EmitRegister(AluOperation.Mov, destination, 0, 0);
        return destination;
    }

    private void EmitCallArguments(
        OberonCompilerObject procedure,
        IReadOnlyList<OberonCompilerExpression> arguments)
    {
        var parameters = procedure.Type?.Parameters ?? [];
        if (arguments.Count != parameters.Count)
            throw new InvalidOperationException($"procedure '{procedure.Name}' expects {parameters.Count} arguments");

        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (IsScalar(parameter.Type))
            {
                if (parameter.ReadOnly) EmitExpression(arguments[index], index);
                else EmitAddress(arguments[index], index);
                continue;
            }

            if (parameter.Type?.Form == OberonCompilerTypeForm.Array
                && parameter.Type.Length < 0)
            {
                EmitOpenArrayArgument(arguments[index], index);
                continue;
            }

            throw new NotSupportedException("record parameter lowering is not implemented");
        }
    }

    private void EmitOpenArrayArgument(OberonCompilerExpression argument, int destination)
    {
        if (argument is not OberonCompilerNameExpression name || name.Symbol is null)
            throw new NotSupportedException("open array arguments must be addressable names");

        EmitAddress(argument, destination);
        var type = name.Symbol.Type;
        if (type?.Form != OberonCompilerTypeForm.Array)
            throw new InvalidOperationException($"'{name.Name}' is not an array");
        if (type.Length >= 0)
        {
            _generator.EmitMoveImmediate(destination + 1, type.Length);
            return;
        }

        if (name.Symbol.Class != OberonCompilerObjectClass.Parameter)
            throw new NotSupportedException("open array length source is not available");
        _generator.EmitLoad(destination + 1, OberonCompilerCodeGenerator.StackPointer,
            checked((int)name.Symbol.Value + 4));
    }

    private void EmitAddress(OberonCompilerExpression expression, int destination)
    {
        if (expression is not OberonCompilerNameExpression name || name.Symbol is null)
            throw new NotSupportedException("VAR arguments must be addressable names");

        var symbol = name.Symbol;
        var offset = checked((int)symbol.Value);
        switch (symbol.Class)
        {
            case OberonCompilerObjectClass.Variable:
                var baseRegister = symbol.Level == 1
                    ? ModuleDataBase
                    : OberonCompilerCodeGenerator.StackPointer;
                _generator.EmitImmediate(AluOperation.Add, destination, baseRegister, offset);
                return;
            case OberonCompilerObjectClass.Parameter when !symbol.ReadOnly:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when symbol.Type?.Form == OberonCompilerTypeForm.Array && symbol.Type.Length < 0:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer, offset);
                return;
            default:
                throw new NotSupportedException($"'{name.Name}' cannot be passed by reference");
        }
    }

    private void EmitReturn()
    {
        if (_frameSize == 0) _generator.EmitReturn();
        else _generator.EmitProcedureReturn(_frameSize);
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

    private static bool IsScalar(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean or
        OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer;

    private static void ValidateScratchRegister(int destination)
    {
        if (destination is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(destination),
                "expression registers must not overlap module base, stack pointer, or link register");
    }
}
