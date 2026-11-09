namespace ProjectOberon.Emulator;

public sealed class OberonCompilerAssignmentEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly Func<OberonCompilerExpression, int, int> _emitExpression;

    public OberonCompilerAssignmentEmitter(
        OberonCompilerCodeGenerator generator,
        OberonCompilerAddressEmitter addresses,
        Func<OberonCompilerExpression, int, int> emitExpression)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(emitExpression);

        _generator = generator;
        _addresses = addresses;
        _emitExpression = emitExpression;
    }

    public void EmitAssignment(OberonCompilerAssignmentStatement assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _emitExpression(assignment.Value, 0);
        if (assignment.Target is OberonCompilerFieldExpression field)
        {
            if (!IsScalar(field.Field.Type))
                throw new NotSupportedException("aggregate field stores are not implemented");
            _addresses.EmitAddress(field, 1);
            _generator.EmitStore(0, 1, 0);
            return;
        }

        if (assignment.Target is not OberonCompilerNameExpression name || name.Symbol is null)
            throw new InvalidOperationException("assignment target is not addressable");
        var target = name.Symbol;
        if (target.Class is not (OberonCompilerObjectClass.Variable or OberonCompilerObjectClass.Parameter))
            throw new InvalidOperationException($"'{name.Name}' is not a variable");

        var offset = checked((int)target.Value);
        if (target.Class == OberonCompilerObjectClass.Parameter && !target.ReadOnly)
        {
            _generator.EmitLoad(1, OberonCompilerCodeGenerator.StackPointer, offset);
            _generator.EmitStore(0, 1, 0);
            return;
        }

        var addressRegister = target.Level == 1
            ? OberonCompilerCallingConvention.ModuleDataBase
            : OberonCompilerCodeGenerator.StackPointer;
        _generator.EmitStore(0, addressRegister, offset);
    }

    private static bool IsScalar(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
        or OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer;
}
