namespace ProjectOberon.Compiler;

public sealed class OberonCompilerControlFlowEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly Func<OberonCompilerExpression, int, int> _emitExpression;
    private readonly Action<OberonCompilerStatementSequence> _emitStatements;
    private readonly Action<OberonCompilerAssignmentStatement> _emitAssignment;

    public OberonCompilerControlFlowEmitter(
        IOberonCompilerInstructionWriter generator,
        Func<OberonCompilerExpression, int, int> emitExpression,
        Action<OberonCompilerStatementSequence> emitStatements,
        Action<OberonCompilerAssignmentStatement> emitAssignment)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(emitExpression);
        ArgumentNullException.ThrowIfNull(emitStatements);
        ArgumentNullException.ThrowIfNull(emitAssignment);

        _generator = generator;
        _emitExpression = emitExpression;
        _emitStatements = emitStatements;
        _emitAssignment = emitAssignment;
    }

    public void EmitIf(OberonCompilerIfStatement conditional)
    {
        ArgumentNullException.ThrowIfNull(conditional);

        if (TryEmitConstantIf(conditional))
        {
            return;
        }

        var end = new OberonCompilerLabel();
        foreach (var branch in conditional.Branches)
        {
            var next = new OberonCompilerLabel();
            EmitBranchIfFalse(branch.Condition, next);
            _emitStatements(branch.Body);
            _generator.EmitBranch(end);
            _generator.Bind(next);
        }

        if (conditional.ElseBody is not null)
        {
            _emitStatements(conditional.ElseBody);
        }
        _generator.Bind(end);
    }

    public void EmitWhile(OberonCompilerWhileStatement loop)
    {
        ArgumentNullException.ThrowIfNull(loop);

        if (TryGetBooleanConstant(loop.Condition, out var condition) && !condition)
        {
            return;
        }

        var start = new OberonCompilerLabel();
        var end = new OberonCompilerLabel();
        _generator.Bind(start);
        EmitBranchIfFalse(loop.Condition, end);
        _emitStatements(loop.Body);
        _generator.EmitBranch(start);
        _generator.Bind(end);
    }

    public void EmitRepeat(OberonCompilerRepeatStatement repeat)
    {
        ArgumentNullException.ThrowIfNull(repeat);
        var start = new OberonCompilerLabel();
        _generator.Bind(start);
        _emitStatements(repeat.Body);

        if (TryGetBooleanConstant(repeat.Condition, out var condition) && condition)
        {
            return;
        }

        EmitBranchIfFalse(repeat.Condition, start);
    }

    public void EmitFor(OberonCompilerForStatement loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        var start = new OberonCompilerLabel();
        var negative = new OberonCompilerLabel();
        var body = new OberonCompilerLabel();
        var end = new OberonCompilerLabel();

        _emitAssignment(new OberonCompilerAssignmentStatement(loop.Control, loop.Start));
        _generator.Bind(start);
        _emitExpression(loop.Step, 2);
        _generator.EmitMoveImmediate(3, 0);
        _generator.EmitRegister(AluOperation.Sub, 2, 2, 3);
        _generator.EmitBranch(negative, ConditionCode.Lt);

        EmitForBoundCheck(loop, end, ConditionCode.Le, invert: true);
        _generator.EmitBranch(body);

        _generator.Bind(negative);
        EmitForBoundCheck(loop, end, ConditionCode.Lt, invert: true);
        _generator.Bind(body);

        _emitStatements(loop.Body);
        var increment = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            loop.Control,
            loop.Step);
        _emitAssignment(new OberonCompilerAssignmentStatement(loop.Control, increment));
        _generator.EmitBranch(start);
        _generator.Bind(end);
    }

    public void EmitCase(OberonCompilerCaseStatement conditional)
    {
        ArgumentNullException.ThrowIfNull(conditional);
        _emitExpression(conditional.Selector, 0);
        var end = new OberonCompilerLabel();
        var armLabels = conditional.Arms
            .Select(_ => new OberonCompilerLabel())
            .ToArray();

        for (var armIndex = 0; armIndex < conditional.Arms.Count; armIndex++)
        {
            foreach (var label in conditional.Arms[armIndex].Labels)
            {
                if (label.Start == label.End)
                {
                    EmitCaseValueDifference(label.Start);
                    _generator.EmitBranch(armLabels[armIndex], ConditionCode.Eq);
                    continue;
                }

                var nextLabel = new OberonCompilerLabel();
                EmitCaseValueDifference(label.Start);
                _generator.EmitBranch(nextLabel, ConditionCode.Lt);
                EmitCaseValueDifference(label.End);
                _generator.EmitBranch(armLabels[armIndex], ConditionCode.Le);
                _generator.Bind(nextLabel);
            }
        }

        if (conditional.ElseBody is not null)
        {
            _emitStatements(conditional.ElseBody);
        }
        _generator.EmitBranch(end);
        for (var armIndex = 0; armIndex < conditional.Arms.Count; armIndex++)
        {
            _generator.Bind(armLabels[armIndex]);
            _emitStatements(conditional.Arms[armIndex].Body);
            _generator.EmitBranch(end);
        }

        _generator.Bind(end);
    }

    private void EmitForBoundCheck(
        OberonCompilerForStatement loop,
        OberonCompilerLabel end,
        ConditionCode condition,
        bool invert)
    {
        _emitExpression(loop.Control, 0);

        if (loop.End is OberonCompilerIntegerLiteralExpression literal
            && literal.Type.Form is OberonCompilerTypeForm.Byte
                or OberonCompilerTypeForm.Character
                or OberonCompilerTypeForm.Integer
            && literal.Value is >= -0x8000 and <= 0xffff)
        {
            _generator.EmitImmediate(
                AluOperation.Sub,
                0,
                0,
                checked((int)literal.Value));
        }
        else
        {
            _emitExpression(loop.End, 1);
            _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        }

        _generator.EmitBranch(end, condition, link: false, invert: invert);
    }

    private void EmitBranchIfFalse(
        OberonCompilerExpression condition,
        OberonCompilerLabel falseLabel)
    {
        if (TryEmitIntegerComparisonBranch(condition, falseLabel))
        {
            return;
        }

        _emitExpression(condition, 0);
        _generator.EmitMoveImmediate(1, 0);
        _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        _generator.EmitBranch(falseLabel, ConditionCode.Eq);
    }

    private void EmitCaseValueDifference(long value)
    {
        _generator.EmitMoveImmediate(1, checked((int)value));
        _generator.EmitRegister(AluOperation.Sub, 2, 0, 1);
    }

    private bool TryEmitConstantIf(OberonCompilerIfStatement conditional)
    {
        foreach (var branch in conditional.Branches)
        {
            if (!TryGetBooleanConstant(branch.Condition, out _))
            {
                return false;
            }
        }

        foreach (var branch in conditional.Branches)
        {
            if (TryGetBooleanConstant(branch.Condition, out var condition) && condition)
            {
                _emitStatements(branch.Body);
                return true;
            }
        }

        if (conditional.ElseBody is not null)
        {
            _emitStatements(conditional.ElseBody);
        }

        return true;
    }

    private static bool TryGetBooleanConstant(
        OberonCompilerExpression expression,
        out bool value)
    {
        if (expression is OberonCompilerIntegerLiteralExpression literal
            && literal.Type.Form == OberonCompilerTypeForm.Boolean
            && literal.Value is 0 or 1)
        {
            value = literal.Value == 1;
            return true;
        }

        value = false;
        return false;
    }

    private bool TryEmitIntegerComparisonBranch(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        if (expression is not OberonCompilerBinaryExpression comparison
            || !IsInteger(comparison.Left)
            || !IsInteger(comparison.Right)
            || comparison.Operator is not (
                OberonCompilerTokenKind.Equal or
                OberonCompilerTokenKind.NotEqual or
                OberonCompilerTokenKind.Less or
                OberonCompilerTokenKind.LessOrEqual or
                OberonCompilerTokenKind.Greater or
                OberonCompilerTokenKind.GreaterOrEqual))
        {
            return false;
        }

        _emitExpression(comparison.Left, 0);
        if (comparison.Right is OberonCompilerIntegerLiteralExpression literal
            && literal.Value is >= -0x8000 and <= 0xffff)
        {
            _generator.EmitImmediate(
                AluOperation.Sub,
                0,
                0,
                checked((int)literal.Value));
        }
        else
        {
            _emitExpression(comparison.Right, 1);
            _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        }

        var (condition, invert) = ComparisonBranch(comparison.Operator);
        _generator.EmitBranch(
            falseLabel,
            condition,
            link: false,
            invert: !invert);
        return true;
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

    private static bool IsInteger(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form is
            OberonCompilerTypeForm.Byte or
            OberonCompilerTypeForm.Character or
            OberonCompilerTypeForm.Integer;
}
