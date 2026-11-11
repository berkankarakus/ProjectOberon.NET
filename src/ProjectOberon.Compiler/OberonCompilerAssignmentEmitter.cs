namespace ProjectOberon.Compiler;

public sealed class OberonCompilerAssignmentEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly OberonCompilerRegisterAllocator _registers;
    private readonly Func<OberonCompilerExpression, int, int> _emitExpression;

    public OberonCompilerAssignmentEmitter(
        IOberonCompilerInstructionWriter generator,
        OberonCompilerAddressEmitter addresses,
        OberonCompilerRegisterAllocator registers,
        Func<OberonCompilerExpression, int, int> emitExpression)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(registers);
        ArgumentNullException.ThrowIfNull(emitExpression);

        _generator = generator;
        _addresses = addresses;
        _registers = registers;
        _emitExpression = emitExpression;
    }

    public void EmitAssignment(OberonCompilerAssignmentStatement assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var targetType = ExpressionType(assignment.Target);
        if (OberonCompilerTypeRules.IsAggregate(targetType))
        {
            if (assignment.Value is OberonCompilerStringLiteralExpression literal
                && targetType!.Form == OberonCompilerTypeForm.Array
                && targetType.Base?.Form == OberonCompilerTypeForm.Character)
            {
                EmitStringCopy(assignment.Target, literal, targetType.Length);
                return;
            }

            EmitAggregateCopy(assignment.Target, assignment.Value, targetType!.Size);
            return;
        }

        _emitExpression(assignment.Value, 0);
        if (assignment.Target is OberonCompilerFieldExpression field)
        {
            if (!OberonCompilerTypeRules.IsScalar(field.Field.Type))
                throw new NotSupportedException("aggregate field stores are not implemented");
            using var preservedValue = _registers.ReserveRange(0, 1);
            _addresses.EmitAddress(field, 1);
            _generator.EmitStore(0, 1, 0, OberonCompilerTypeRules.IsByteSized(field.Field.Type));
            return;
        }

        if (assignment.Target is OberonCompilerIndexExpression index)
        {
            if (!OberonCompilerTypeRules.IsScalar(index.ElementType))
                throw new NotSupportedException("aggregate array element stores are not implemented");
            using var preservedValue = _registers.ReserveRange(0, 1);
            _addresses.EmitAddress(index, 1);
            _generator.EmitStore(0, 1, 0, OberonCompilerTypeRules.IsByteSized(index.ElementType));
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
            _addresses.EmitAddress(name, 1);
            _generator.EmitStore(0, 1, 0, OberonCompilerTypeRules.IsByteSized(target.Type));
            return;
        }

        var addressRegister = _addresses.EmitStorageBase(target, 1);
        _generator.EmitStore(0, addressRegister, offset, OberonCompilerTypeRules.IsByteSized(target.Type));
    }

    private void EmitAggregateCopy(
        OberonCompilerExpression target,
        OberonCompilerExpression source,
        int size)
    {
        _addresses.EmitAddress(source, 1);
        using var protectedSource = _registers.ReserveRange(1, 1);
        _addresses.EmitAddress(target, 2);

        var offset = 0;
        while (offset + 4 <= size)
        {
            _generator.EmitLoad(0, 1, offset);
            _generator.EmitStore(0, 2, offset);
            offset += 4;
        }

        while (offset < size)
        {
            _generator.EmitLoad(0, 1, offset, byteValue: true);
            _generator.EmitStore(0, 2, offset, byteValue: true);
            offset++;
        }
    }

    private void EmitStringCopy(
        OberonCompilerExpression target,
        OberonCompilerStringLiteralExpression source,
        int targetLength)
    {
        _addresses.EmitAddress(source, 1);
        using var protectedSource = _registers.ReserveRange(1, 1);
        _addresses.EmitAddress(target, 2);

        var copied = Math.Min(source.Value.Length, targetLength);
        for (var offset = 0; offset < copied; offset++)
        {
            _generator.EmitLoad(0, 1, offset, byteValue: true);
            _generator.EmitStore(0, 2, offset, byteValue: true);
        }

        for (var offset = copied; offset < targetLength; offset++)
        {
            _generator.EmitMoveImmediate(0, 0);
            _generator.EmitStore(0, 2, offset, byteValue: true);
        }
    }

    private static OberonCompilerType? ExpressionType(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerNameExpression name => name.Symbol?.Type,
        OberonCompilerFieldExpression field => field.Field.Type,
        OberonCompilerIndexExpression index => index.ElementType,
        _ => null
    };

}
