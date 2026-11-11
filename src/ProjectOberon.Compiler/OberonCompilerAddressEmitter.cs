namespace ProjectOberon.Compiler;

public sealed class OberonCompilerAddressEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly Func<OberonCompilerExpression, int, int>? _emitExpression;
    private readonly OberonCompilerRegisterAllocator? _registers;
    private readonly Func<string, int>? _stringAddressResolver;
    private readonly int _currentLevel;

    public OberonCompilerAddressEmitter(
        IOberonCompilerInstructionWriter generator,
        Func<OberonCompilerExpression, int, int>? emitExpression = null,
        OberonCompilerRegisterAllocator? registers = null,
        int currentLevel = 1,
        Func<string, int>? stringAddressResolver = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _emitExpression = emitExpression;
        _registers = registers;
        _currentLevel = currentLevel;
        _stringAddressResolver = stringAddressResolver;
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
                var baseRegister = EmitStorageBase(symbol, destination);
                _generator.EmitImmediate(AluOperation.Add, destination, baseRegister, offset);
                return;
            case OberonCompilerObjectClass.Parameter when !symbol.ReadOnly:
                var referenceBase = EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, referenceBase, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when symbol.Type?.Form == OberonCompilerTypeForm.Array && symbol.Type.Length < 0:
                var arrayBase = EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, arrayBase, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when OberonCompilerTypeRules.IsAggregate(symbol.Type):
                var aggregateBase = EmitStorageBase(symbol, destination);
                _generator.EmitLoad(destination, aggregateBase, offset);
                return;
            default:
                throw new NotSupportedException($"'{name.Name}' cannot be passed by reference");
        }
    }

    public int EmitStorageBase(OberonCompilerObject symbol, int destination)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (symbol.Level == 1) return OberonCompilerCallingConvention.ModuleDataBase;
        if (symbol.Level == _currentLevel) return OberonCompilerCallingConvention.StackPointer;
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
