namespace ProjectOberon.Compiler;

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
        Func<OberonCompilerObject, OberonCompilerLabel?>? procedureLabelResolver = null,
        int currentLevel = 1,
        Func<string, int>? stringAddressResolver = null)
        : this(
            generator,
            frameSize,
            procedureLabelResolver,
            currentLevel,
            stringAddressResolver,
            externalProcedureEmitter: null)
    {
    }

    internal OberonCompilerStatementEmitter(
        IOberonCompilerInstructionWriter generator,
        int frameSize,
        Func<OberonCompilerObject, OberonCompilerLabel?>? procedureLabelResolver,
        int currentLevel,
        Func<string, int>? stringAddressResolver,
        Action<OberonCompilerObject>? externalProcedureEmitter)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _frameSize = frameSize;
        _frames = new OberonCompilerFrameEmitter(generator);

        var registers = new OberonCompilerRegisterAllocator();
        OberonCompilerExpressionEmitter? expressions = null;
        var addresses = new OberonCompilerAddressEmitter(
            generator,
            (expression, destination) => expressions!.EmitExpression(expression, destination),
            registers,
            currentLevel,
            stringAddressResolver);
        OberonCompilerCallEmitter? calls = null;
        expressions = new OberonCompilerExpressionEmitter(
            generator,
            addresses,
            registers,
            (call, destination) => calls!.EmitFunctionCall(call, destination));
        _expressions = expressions;
        calls = new OberonCompilerCallEmitter(
            generator,
            addresses,
            registers,
            _expressions.EmitExpression,
            procedureLabelResolver,
            currentLevel,
            externalProcedureEmitter);
        _calls = calls;
        _assignments = new OberonCompilerAssignmentEmitter(
            generator,
            addresses,
            registers,
            _expressions.EmitExpression);
        _controlFlow = new OberonCompilerControlFlowEmitter(
            generator,
            _expressions.EmitExpression,
            EmitStatementSequence,
            _assignments.EmitAssignment);
    }

    public void EmitAssignment(OberonCompilerAssignmentStatement assignment) =>
        _assignments.EmitAssignment(assignment);

    public void EmitStatementSequence(OberonCompilerStatementSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        foreach (var statement in sequence.Statements)
        {
            EmitStatement(statement);
        }
    }

    private void EmitStatement(OberonCompilerStatement statement)
    {
        switch (statement)
        {
            case OberonCompilerAssignmentStatement assignment:
                _assignments.EmitAssignment(assignment);
                break;
            case OberonCompilerReturnStatement { Value: var value }:
                if (value is not null)
                {
                    _expressions.EmitExpression(value);
                }
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
            case OberonCompilerForStatement loop:
                _controlFlow.EmitFor(loop);
                break;
            case OberonCompilerCaseStatement conditional:
                _controlFlow.EmitCase(conditional);
                break;
            case OberonCompilerCallStatement call:
                _calls.EmitCall(call);
                break;
            default:
                throw new NotSupportedException($"unsupported statement: {statement.GetType().Name}");
        }
    }
}
