namespace ProjectOberon.Compiler;

public sealed class OberonCompilerAddressEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly Func<OberonCompilerExpression, int, int>? _emitExpression;
    private readonly OberonCompilerRegisterAllocator? _registers;
    private readonly Func<string, int>? _stringAddressResolver;
    private readonly Action<OberonCompilerObject, int>? _externalVariableAddressEmitter;
    private readonly int _currentLevel;
    private readonly int _moduleDataOffset;

    public OberonCompilerAddressEmitter(
        IOberonCompilerInstructionWriter generator,
        Func<OberonCompilerExpression, int, int>? emitExpression = null,
        OberonCompilerRegisterAllocator? registers = null,
        int currentLevel = 1,
        Func<string, int>? stringAddressResolver = null)
        : this(
            generator,
            emitExpression,
            registers,
            currentLevel,
            stringAddressResolver,
            externalVariableAddressEmitter: null,
            moduleDataOffset: 0)
    {
    }

    internal OberonCompilerAddressEmitter(
        IOberonCompilerInstructionWriter generator,
        Func<OberonCompilerExpression, int, int>? emitExpression,
        OberonCompilerRegisterAllocator? registers,
        int currentLevel,
        Func<string, int>? stringAddressResolver,
        Action<OberonCompilerObject, int>? externalVariableAddressEmitter,
        int moduleDataOffset)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _emitExpression = emitExpression;
        _registers = registers;
        _currentLevel = currentLevel;
        _stringAddressResolver = stringAddressResolver;
        _externalVariableAddressEmitter = externalVariableAddressEmitter;
        _moduleDataOffset = moduleDataOffset;
    }

    public void EmitAddress(OberonCompilerExpression expression, int destination)
    {
        ArgumentNullException.ThrowIfNull(expression);
        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);

        if (expression is OberonCompilerStringLiteralExpression literal)
        {
            if (_stringAddressResolver is null)
                throw new InvalidOperationException("string literal lowering requires a module data resolver");
            _generator.EmitImmediate(
                AluOperation.Add,
                destination,
                OberonCompilerCallingConvention.ModuleDataBase,
                _stringAddressResolver(literal.Value));
            return;
        }

        if (expression is OberonCompilerIndexExpression index)
        {
            EmitIndexedAddress(index, destination);
            return;
        }

        if (expression is OberonCompilerDereferenceExpression dereference)
        {
            if (_emitExpression is null)
                throw new InvalidOperationException("pointer dereference lowering requires an expression emitter");
            _emitExpression(dereference.Receiver, destination);
            return;
        }

        if (expression is OberonCompilerCallExpression call
            && OberonCompilerTypeRules.IsAggregate(call.Procedure.Symbol?.Type?.Base))
        {
            if (_emitExpression is null)
            {
                throw new InvalidOperationException(
                    "aggregate function result lowering requires an expression emitter");
            }

            _emitExpression(call, destination);
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
        if (symbol.Level == 1)
        {
            offset = checked(offset + _moduleDataOffset);
        }

        switch (symbol.Class)
        {
            case OberonCompilerObjectClass.Variable:
                if (symbol.Level == 0)
                {
                    if (_externalVariableAddressEmitter is null)
                    {
                        throw new NotSupportedException(
                            $"external variable '{symbol.Name}' requires RSC data fixup lowering");
                    }

                    _externalVariableAddressEmitter(symbol, destination);
                    return;
                }

                var baseRegister = EmitStorageBase(symbol, destination);
                _generator.EmitImmediate(AluOperation.Add, destination, baseRegister, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when RequiresIndirectParameterLoad(symbol):
                var parameterBase = EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, parameterBase, offset);
                return;
            default:
                throw new NotSupportedException($"'{name.Name}' cannot be passed by reference");
        }
    }

    public int EmitStorageBase(OberonCompilerObject symbol, int destination)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (symbol.Level == 1)
        {
            return OberonCompilerCallingConvention.ModuleDataBase;
        }
        if (symbol.Level == _currentLevel)
        {
            return OberonCompilerCallingConvention.StackPointer;
        }
        if (symbol.Level > _currentLevel)
            throw new InvalidOperationException($"symbol '{symbol.Name}' is in a deeper scope");

        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);
        _generator.EmitRegister(
            AluOperation.Mov,
            destination,
            OberonCompilerCallingConvention.StackPointer,
            OberonCompilerCallingConvention.StackPointer);
        for (var level = _currentLevel; level > symbol.Level; level--)
        {
            _generator.EmitLoad(
                destination,
                destination,
                OberonCompilerCallingConvention.StaticLinkOffset);
        }

        return destination;
    }

    private static bool RequiresIndirectParameterLoad(OberonCompilerObject symbol)
    {
        if (!symbol.ReadOnly)
        {
            return true;
        }

        if (OberonCompilerTypeRules.IsOpenArray(symbol.Type))
        {
            return true;
        }

        return OberonCompilerTypeRules.IsAggregate(symbol.Type);
    }

    private void EmitIndexedAddress(OberonCompilerIndexExpression index, int destination)
    {
        if (_emitExpression is null || _registers is null)
            throw new InvalidOperationException("array index lowering requires an expression emitter");

        if (TryEmitConstantIndexAddress(index, destination))
        {
            return;
        }

        using var protectedDestination = _registers.ReserveRangeScope(destination, 1);
        var indexRegister = _registers.AcquireTemporary(destination);
        try
        {
            EmitAddress(index.Receiver, destination);
            _emitExpression(index.Index, indexRegister);
            if (index.ElementType.Size != 1)
            {
                EmitElementOffset(indexRegister, index.ElementType.Size);
            }

            _generator.EmitRegister(AluOperation.Add, destination, destination, indexRegister);
        }
        finally
        {
            _registers.ReleaseTemporary(indexRegister);
        }
    }

    private bool TryEmitConstantIndexAddress(
        OberonCompilerIndexExpression index,
        int destination)
    {
        if (index.Index is not OberonCompilerIntegerLiteralExpression literal)
        {
            return false;
        }

        long offset;
        try
        {
            offset = checked(literal.Value * index.ElementType.Size);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (offset is < -0x8000 or > 0xffff)
        {
            return false;
        }

        EmitAddress(index.Receiver, destination);
        if (offset != 0)
        {
            _generator.EmitImmediate(
                AluOperation.Add,
                destination,
                destination,
                checked((int)offset));
        }

        return true;
    }

    private void EmitElementOffset(int indexRegister, int elementSize)
    {
        if (elementSize > 1 && (elementSize & (elementSize - 1)) == 0)
        {
            var shift = 0;
            for (var scale = elementSize; scale > 1; scale >>= 1)
            {
                shift++;
            }

            _generator.EmitImmediate(
                AluOperation.Lsl,
                indexRegister,
                indexRegister,
                shift);
            return;
        }

        var sizeRegister = _registers!.AcquireTemporary(indexRegister);
        try
        {
            _generator.EmitMoveImmediate(sizeRegister, elementSize);
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
}
