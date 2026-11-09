namespace ProjectOberon.Emulator;

public sealed class OberonCompilerControlFlowEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;
    private readonly Func<OberonCompilerExpression, int, int> _emitExpression;
    private readonly Action<OberonCompilerStatementSequence> _emitStatements;

    public OberonCompilerControlFlowEmitter(
        OberonCompilerCodeGenerator generator,
        Func<OberonCompilerExpression, int, int> emitExpression,
        Action<OberonCompilerStatementSequence> emitStatements)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(emitExpression);
        ArgumentNullException.ThrowIfNull(emitStatements);

        _generator = generator;
        _emitExpression = emitExpression;
        _emitStatements = emitStatements;
    }

    public void EmitIf(OberonCompilerIfStatement conditional)
    {
        ArgumentNullException.ThrowIfNull(conditional);
        var end = new OberonCompilerLabel();
        foreach (var branch in conditional.Branches)
        {
            var next = new OberonCompilerLabel();
            EmitBranchIfFalse(branch.Condition, next);
            _emitStatements(branch.Body);
            _generator.EmitBranch(end);
            _generator.Bind(next);
        }

        if (conditional.ElseBody is not null) _emitStatements(conditional.ElseBody);
        _generator.Bind(end);
    }

    public void EmitWhile(OberonCompilerWhileStatement loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
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
        EmitBranchIfFalse(repeat.Condition, start);
    }

    private void EmitBranchIfFalse(
        OberonCompilerExpression condition,
        OberonCompilerLabel falseLabel)
    {
        _emitExpression(condition, 0);
        _generator.EmitMoveImmediate(1, 0);
        _generator.EmitRegister(AluOperation.Sub, 0, 0, 1);
        _generator.EmitBranch(falseLabel, ConditionCode.Eq);
    }
}
