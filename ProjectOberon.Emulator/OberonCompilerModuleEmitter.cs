namespace ProjectOberon.Emulator;

public sealed class OberonCompilerModuleEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;
    private readonly OberonCompilerExpressionEmitter _statements;

    public OberonCompilerModuleEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _statements = new OberonCompilerExpressionEmitter(generator);
    }

    public IReadOnlyList<uint> Emit(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!module.Succeeded) throw new InvalidOperationException("cannot emit an invalid module");

        var procedures = module.ModuleScope!.Objects
            .Where(symbol => symbol.Class == OberonCompilerObjectClass.Procedure)
            .ToArray();
        foreach (var procedure in procedures) procedure.CodeLabel = new OberonCompilerLabel();

        foreach (var procedure in procedures)
        {
            _generator.Bind(procedure.CodeLabel!);
            _generator.EmitProcedurePrologue(procedure.FrameSize);
            SaveParameters(procedure);
            var procedureStatements = new OberonCompilerExpressionEmitter(_generator, procedure.FrameSize);
            if (procedure.Body is not null) procedureStatements.EmitStatementSequence(procedure.Body);
            if (!EndsWithReturn(procedure.Body)) _generator.EmitProcedureReturn(procedure.FrameSize);
        }

        if (module.Body is not null) _statements.EmitStatementSequence(module.Body);
        return _generator.Code;
    }

    private void SaveParameters(OberonCompilerObject procedure)
    {
        var register = 0;
        foreach (var parameter in procedure.Type!.Parameters)
        {
            if (parameter.Type?.Form is (
                OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
                or OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer))
            {
                _generator.EmitSaveParameter(register, checked((int)parameter.Value));
                register++;
                continue;
            }

            if (parameter.Type?.Form == OberonCompilerTypeForm.Array
                && parameter.Type.Length < 0)
            {
                _generator.EmitSaveParameter(register, checked((int)parameter.Value));
                _generator.EmitSaveParameter(register + 1, checked((int)parameter.Value + 4));
                register += 2;
                continue;
            }

            throw new NotSupportedException("aggregate parameter lowering is not implemented");
        }
    }

    private static bool EndsWithReturn(OberonCompilerStatementSequence? body) =>
        body?.Statements.LastOrDefault() is OberonCompilerReturnStatement;
}
