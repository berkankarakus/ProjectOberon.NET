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
        var hasConstantStep = TryGetIntegerStep(loop.Step, out var constantStep, out var stepType)
            && constantStep != 0;

        _emitAssignment(new OberonCompilerAssignmentStatement(loop.Control, loop.Start));
        _generator.Bind(start);

        if (hasConstantStep)
        {
            var condition = constantStep > 0 ? ConditionCode.Le : ConditionCode.Lt;
            EmitForBoundCheck(loop, end, condition, invert: true);
        }
        else
        {
            _emitExpression(loop.Step, 2);
            _generator.EmitMoveImmediate(3, 0);
            _generator.EmitRegister(AluOperation.Sub, 2, 2, 3);
            _generator.EmitBranch(negative, ConditionCode.Lt);

            EmitForBoundCheck(loop, end, ConditionCode.Le, invert: true);
            _generator.EmitBranch(body);

            _generator.Bind(negative);
            EmitForBoundCheck(loop, end, ConditionCode.Lt, invert: true);
            _generator.Bind(body);
        }

        _emitStatements(loop.Body);
        var incrementStep = hasConstantStep
            ? new OberonCompilerIntegerLiteralExpression(stepType!, constantStep)
            : loop.Step;
        var increment = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            loop.Control,
            incrementStep);
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
        if (TryEmitConjunctionBranch(condition, falseLabel))
        {
            return;
        }

        if (TryEmitDisjunctionBranch(condition, falseLabel))
        {
            return;
        }

        if (condition is OberonCompilerUnaryExpression unary
            && unary.Operator == OberonCompilerTokenKind.Not)
        {
            if (TryEmitIntegerComparisonBranch(unary.Operand, falseLabel, branchWhenTrue: true))
            {
                return;
            }

            _emitExpression(unary.Operand, 0);
            _generator.EmitImmediate(AluOperation.Sub, 0, 0, 0);
            _generator.EmitBranch(falseLabel, ConditionCode.Eq, link: false, invert: true);
            return;
        }

        if (TryEmitSetMembershipBranch(condition, falseLabel))
        {
            return;
        }

        if (TryEmitRealComparisonBranch(condition, falseLabel))
        {
            return;
        }

        if (TryEmitIntegerComparisonBranch(condition, falseLabel))
        {
            return;
        }

        _emitExpression(condition, 0);
        _generator.EmitImmediate(AluOperation.Sub, 0, 0, 0);
        _generator.EmitBranch(falseLabel, ConditionCode.Eq);
    }

    private bool TryEmitConjunctionBranch(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        if (expression is not OberonCompilerBinaryExpression conjunction
            || conjunction.Operator != OberonCompilerTokenKind.And
            || !IsDirectBranchCondition(conjunction.Left)
            || !IsDirectBranchCondition(conjunction.Right))
        {
            return false;
        }

        TryEmitBranchableCondition(conjunction.Left, falseLabel);
        TryEmitBranchableCondition(conjunction.Right, falseLabel);
        return true;
    }

    private bool TryEmitDisjunctionBranch(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        if (expression is not OberonCompilerBinaryExpression disjunction
            || disjunction.Operator != OberonCompilerTokenKind.Or
            || !IsDirectBranchCondition(disjunction.Left)
            || !IsDirectBranchCondition(disjunction.Right))
        {
            return false;
        }

        var rightLabel = new OberonCompilerLabel();
        var trueLabel = new OberonCompilerLabel();
        TryEmitBranchableCondition(disjunction.Left, rightLabel);
        _generator.EmitBranch(trueLabel);
        _generator.Bind(rightLabel);
        TryEmitBranchableCondition(disjunction.Right, falseLabel);
        _generator.Bind(trueLabel);
        return true;
    }

    private static bool IsDirectBranchCondition(OberonCompilerExpression expression)
    {
        if (expression is not OberonCompilerBinaryExpression condition
            || !IsSideEffectFreeBranchOperand(condition.Left)
            || !IsSideEffectFreeBranchOperand(condition.Right))
        {
            return false;
        }

        if (condition.Operator == OberonCompilerTokenKind.In)
        {
            return IsInteger(condition.Left) && IsSet(condition.Right);
        }

        var isComparison = condition.Operator is OberonCompilerTokenKind.Equal or
            OberonCompilerTokenKind.NotEqual or
            OberonCompilerTokenKind.Less or
            OberonCompilerTokenKind.LessOrEqual or
            OberonCompilerTokenKind.Greater or
            OberonCompilerTokenKind.GreaterOrEqual;
        return isComparison
            && (IsInteger(condition.Left) && IsInteger(condition.Right)
                || IsReal(condition.Left) && IsReal(condition.Right));
    }

    private bool TryEmitBranchableCondition(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        return TryEmitSetMembershipBranch(expression, falseLabel)
            || TryEmitRealComparisonBranch(expression, falseLabel)
            || TryEmitIntegerComparisonBranch(expression, falseLabel);
    }

    private static bool IsSideEffectFreeBranchOperand(OberonCompilerExpression expression) =>
        expression is OberonCompilerNameExpression
            or OberonCompilerIntegerLiteralExpression
            or OberonCompilerRealLiteralExpression;

    private void EmitCaseValueDifference(long value)
    {
        if (value is >= -0x8000 and <= 0xffff)
        {
            _generator.EmitImmediate(
                AluOperation.Sub,
                2,
                0,
                checked((int)value));
            return;
        }

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
        OberonCompilerLabel falseLabel,
        bool branchWhenTrue = false)
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

        var comparisonOperator = comparison.Operator;
        if (comparison.Right is OberonCompilerIntegerLiteralExpression literal)
        {
            _emitExpression(comparison.Left, 0);
            if (literal.Value is >= -0x8000 and <= 0xffff)
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
        }
        else if (comparison.Left is OberonCompilerIntegerLiteralExpression leftLiteral
            && leftLiteral.Value is >= -0x8000 and <= 0xffff)
        {
            comparisonOperator = ReverseComparison(comparisonOperator);
            _emitExpression(comparison.Right, 0);
            _generator.EmitImmediate(
                AluOperation.Sub,
                0,
                0,
                checked((int)leftLiteral.Value));
        }
        else
        {
            _emitExpression(comparison.Right, 1);
            _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        }

        var (condition, invert) = ComparisonBranch(comparisonOperator);
        _generator.EmitBranch(
            falseLabel,
            condition,
            link: false,
            invert: branchWhenTrue ? invert : !invert);
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

    private bool TryEmitRealComparisonBranch(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        if (expression is not OberonCompilerBinaryExpression comparison
            || !IsReal(comparison.Left)
            || !IsReal(comparison.Right)
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
        _emitExpression(comparison.Right, 1);
        _generator.EmitRegister(AluOperation.Fsb, 0, 0, 1);

        switch (comparison.Operator)
        {
            case OberonCompilerTokenKind.Equal:
                _generator.EmitBranch(falseLabel, ConditionCode.Eq, link: false, invert: true);
                break;
            case OberonCompilerTokenKind.NotEqual:
                _generator.EmitBranch(falseLabel, ConditionCode.Eq);
                break;
            case OberonCompilerTokenKind.Less:
                _generator.EmitBranch(falseLabel, ConditionCode.Mi, link: false, invert: true);
                break;
            case OberonCompilerTokenKind.LessOrEqual:
                var negative = new OberonCompilerLabel();
                _generator.EmitBranch(negative, ConditionCode.Mi);
                _generator.EmitBranch(falseLabel, ConditionCode.Eq, link: false, invert: true);
                _generator.Bind(negative);
                break;
            case OberonCompilerTokenKind.Greater:
                _generator.EmitBranch(falseLabel, ConditionCode.Mi);
                _generator.EmitBranch(falseLabel, ConditionCode.Eq);
                break;
            case OberonCompilerTokenKind.GreaterOrEqual:
                _generator.EmitBranch(falseLabel, ConditionCode.Mi);
                break;
        }

        return true;
    }

    private bool TryEmitSetMembershipBranch(
        OberonCompilerExpression expression,
        OberonCompilerLabel falseLabel)
    {
        if (expression is not OberonCompilerBinaryExpression membership
            || membership.Operator != OberonCompilerTokenKind.In
            || !IsInteger(membership.Left)
            || !IsSet(membership.Right))
        {
            return false;
        }

        _emitExpression(membership.Left, 0);
        if (TryGetImmediateSetLiteral(membership.Right, out var mask))
        {
            _generator.EmitImmediate(AluOperation.And, 0, 0, mask);
            _generator.EmitBranch(falseLabel, ConditionCode.Eq);
            return true;
        }

        _emitExpression(membership.Right, 1);
        _generator.EmitRegister(AluOperation.And, 0, 0, 1);
        _generator.EmitBranch(falseLabel, ConditionCode.Eq);
        return true;
    }

    private static bool TryGetImmediateSetLiteral(
        OberonCompilerExpression expression,
        out int value)
    {
        if (expression is OberonCompilerIntegerLiteralExpression literal
            && literal.Type.Form == OberonCompilerTypeForm.Set
            && literal.Value is >= -0x8000 and <= 0xffff)
        {
            value = checked((int)literal.Value);
            return true;
        }

        value = 0;
        return false;
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

    private static bool IsReal(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form == OberonCompilerTypeForm.Real;

    private static bool IsSet(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form == OberonCompilerTypeForm.Set;

    private static bool TryGetIntegerStep(
        OberonCompilerExpression expression,
        out int value,
        out OberonCompilerType? type)
    {
        if (expression is OberonCompilerIntegerLiteralExpression literal
            && literal.Type.Form is OberonCompilerTypeForm.Byte
                or OberonCompilerTypeForm.Character
                or OberonCompilerTypeForm.Integer
            && literal.Value is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)literal.Value;
            type = literal.Type;
            return true;
        }

        if (expression is OberonCompilerUnaryExpression unary
            && unary.Operator is OberonCompilerTokenKind.Plus or OberonCompilerTokenKind.Minus
            && TryGetIntegerStep(unary.Operand, out var operand, out type))
        {
            if (unary.Operator == OberonCompilerTokenKind.Plus)
            {
                value = operand;
                return true;
            }

            var negated = -(long)operand;
            if (negated is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)negated;
                return true;
            }
        }

        value = 0;
        type = null;
        return false;
    }
}
