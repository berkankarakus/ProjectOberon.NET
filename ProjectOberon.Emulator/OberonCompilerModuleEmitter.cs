namespace ProjectOberon.Emulator;

public sealed class OberonCompilerModuleEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;
    private readonly OberonCompilerExpressionEmitter _statements;
    private readonly Dictionary<OberonCompilerObject, OberonCompilerLabel> _procedureLabels = [];

    public OberonCompilerModuleEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _statements = new OberonCompilerExpressionEmitter(generator, procedureLabelResolver: ResolveProcedureLabel);
    }

    public IReadOnlyList<uint> Emit(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!module.Succeeded) throw new InvalidOperationException("cannot emit an invalid module");

        _procedureLabels.Clear();
        var procedures = module.Procedures ?? [];
        var allProcedures = Flatten(procedures).ToArray();
        foreach (var procedure in allProcedures)
            _procedureLabels[procedure.Symbol] = new OberonCompilerLabel();

        foreach (var procedure in allProcedures)
        {
            _generator.Bind(_procedureLabels[procedure.Symbol]);
            _generator.EmitProcedurePrologue(procedure.FrameSize);
            SaveParameters(procedure.Symbol);
            var procedureStatements = new OberonCompilerExpressionEmitter(
                _generator,
                procedure.FrameSize,
                ResolveProcedureLabel);
            if (procedure.Body is not null) procedureStatements.EmitStatementSequence(procedure.Body);
            if (OberonCompilerControlFlowAnalyzer.CanCompleteNormally(procedure.Body))
                _generator.EmitProcedureReturn(procedure.FrameSize);
        }

        if (module.Body is not null) _statements.EmitStatementSequence(module.Body);
        return _generator.Code;
    }

    private OberonCompilerLabel? ResolveProcedureLabel(OberonCompilerObject procedure) =>
        _procedureLabels.TryGetValue(procedure, out var label) ? label : null;

    private static IEnumerable<OberonCompilerProcedureDefinition> Flatten(
        IEnumerable<OberonCompilerProcedureDefinition> procedures)
    {
        foreach (var procedure in procedures)
        {
            yield return procedure;
            foreach (var nested in Flatten(procedure.NestedProcedures)) yield return nested;
        }
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

}
