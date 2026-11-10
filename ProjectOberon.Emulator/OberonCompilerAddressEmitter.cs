namespace ProjectOberon.Emulator;

public sealed class OberonCompilerAddressEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly Func<OberonCompilerExpression, int, int>? _emitExpression;
    private readonly OberonCompilerRegisterAllocator? _registers;

    public OberonCompilerAddressEmitter(
        IOberonCompilerInstructionWriter generator,
        Func<OberonCompilerExpression, int, int>? emitExpression = null,
        OberonCompilerRegisterAllocator? registers = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _emitExpression = emitExpression;
        _registers = registers;
    }

    public void EmitAddress(OberonCompilerExpression expression, int destination)
    {
        ArgumentNullException.ThrowIfNull(expression);
        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);

        if (expression is OberonCompilerIndexExpression index)
        {
            EmitIndexedAddress(index, destination);
            return;
        }

        if (expression is OberonCompilerFieldExpression field)
        {
            EmitAddress(field.Receiver, destination);
            _generator.EmitImmediate(AluOperation.Add, destination, destination,
                checked((int)field.Field.Value));
            return;
        }

        if (expression is not OberonCompilerNameExpression name || name.Symbol is null)
            throw new NotSupportedException("argument must be an addressable name");

        var symbol = name.Symbol;
        var offset = checked((int)symbol.Value);
        switch (symbol.Class)
        {
            case OberonCompilerObjectClass.Variable:
                var baseRegister = symbol.Level == 1
                    ? OberonCompilerCallingConvention.ModuleDataBase
                    : OberonCompilerCallingConvention.StackPointer;
                _generator.EmitImmediate(AluOperation.Add, destination, baseRegister, offset);
                return;
            case OberonCompilerObjectClass.Parameter when !symbol.ReadOnly:
                _generator.EmitLoad(destination, OberonCompilerCallingConvention.StackPointer, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when symbol.Type?.Form == OberonCompilerTypeForm.Array && symbol.Type.Length < 0:
                _generator.EmitLoad(destination, OberonCompilerCallingConvention.StackPointer, offset);
                return;
            default:
                throw new NotSupportedException($"'{name.Name}' cannot be passed by reference");
        }
    }

    private void EmitIndexedAddress(OberonCompilerIndexExpression index, int destination)
    {
        if (_emitExpression is null || _registers is null)
            throw new InvalidOperationException("array index lowering requires an expression emitter");

        using var protectedDestination = _registers.ReserveRange(destination, 1);
        var indexRegister = _registers.AcquireTemporary(destination);
        try
        {
            EmitAddress(index.Receiver, destination);
            _emitExpression(index.Index, indexRegister);
            if (index.ElementType.Size != 1)
            {
                var sizeRegister = _registers.AcquireTemporary(indexRegister);
                try
                {
                    _generator.EmitMoveImmediate(sizeRegister, index.ElementType.Size);
                    _generator.EmitRegister(
                        AluOperation.Mul,
                        indexRegister,
                        indexRegister,
                        sizeRegister);
                }
                finally
                {
                    _registers.ReleaseTemporary(sizeRegister);
                }
            }

            _generator.EmitRegister(AluOperation.Add, destination, destination, indexRegister);
        }
        finally
        {
            _registers.ReleaseTemporary(indexRegister);
        }
    }
}
