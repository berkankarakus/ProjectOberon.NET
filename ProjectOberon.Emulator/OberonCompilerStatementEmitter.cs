namespace ProjectOberon.Emulator;

public sealed class OberonCompilerStatementEmitter
{
    private readonly OberonCompilerExpressionEmitter _expressions;
    private readonly OberonCompilerAssignmentEmitter _assignments;
    private readonly OberonCompilerCallEmitter _calls;
    private readonly OberonCompilerControlFlowEmitter _controlFlow;
    private readonly OberonCompilerFrameEmitter _frames;
    private readonly int _frameSize;

    public OberonCompilerStatementEmitter(
        IOberonCompilerInstructionWriter generator,
        int frameSize = 0,
        Func<OberonCompilerObject, OberonCompilerLabel?>? procedureLabelResolver = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _frameSize = frameSize;
        _frames = new OberonCompilerFrameEmitter(generator);

        var addresses = new OberonCompilerAddressEmitter(generator);
        var registers = new OberonCompilerRegisterAllocator();
        OberonCompilerCallEmitter? calls = null;
        _expressions = new OberonCompilerExpressionEmitter(
            generator,
            addresses,
            registers,
            (call, destination) => calls!.EmitFunctionCall(call, destination));
        calls = new OberonCompilerCallEmitter(
            generator,
            addresses,
            registers,
            _expressions.EmitExpression,
            procedureLabelResolver);
        _calls = calls;
        _assignments = new OberonCompilerAssignmentEmitter(
            generator,
            addresses,
            _expressions.EmitExpression);
        _controlFlow = new OberonCompilerControlFlowEmitter(
            generator,
            _expressions.EmitExpression,
            EmitStatementSequence);
    }

    public void EmitAssignment(OberonCompilerAssignmentStatement assignment) =>
        _assignments.EmitAssignment(assignment);

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
                _assignments.EmitAssignment(assignment);
                break;
            case OberonCompilerReturnStatement { Value: var value }:
                if (value is not null) _expressions.EmitExpression(value);
                _frames.EmitReturn(_frameSize);
                break;
            case OberonCompilerIfStatement conditional:
                _controlFlow.EmitIf(conditional);
                break;
            case OberonCompilerWhileStatement loop:
                _controlFlow.EmitWhile(loop);
                break;
            case OberonCompilerRepeatStatement repeat:
                _controlFlow.EmitRepeat(repeat);
                break;
            case OberonCompilerCallStatement call:
                _calls.EmitCall(call);
                break;
            default:
                throw new NotSupportedException($"unsupported statement: {statement.GetType().Name}");
        }
    }
}
