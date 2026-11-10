namespace ProjectOberon.Emulator;

public sealed class OberonCompilerFrameEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;

    public OberonCompilerFrameEmitter(IOberonCompilerInstructionWriter generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    public void EmitProcedurePrologue(int frameSize, bool hasStaticLink = false)
    {
        _generator.EmitProcedurePrologue(frameSize);
        if (hasStaticLink)
        {
            _generator.EmitSaveParameter(
                OberonCompilerCallingConvention.StaticLinkRegister,
                OberonCompilerCallingConvention.StaticLinkOffset);
        }
    }

    public void EmitProcedureReturn(int frameSize) =>
        _generator.EmitProcedureReturn(frameSize);

    public void EmitReturn(int frameSize)
    {
        if (frameSize == 0) _generator.EmitReturn();
        else _generator.EmitProcedureReturn(frameSize);
    }

    public void SaveParameters(OberonCompilerObject procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        var register = 0;
        foreach (var parameter in procedure.Type!.Parameters)
        {
            if (OberonCompilerTypeRules.IsScalar(parameter.Type))
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
