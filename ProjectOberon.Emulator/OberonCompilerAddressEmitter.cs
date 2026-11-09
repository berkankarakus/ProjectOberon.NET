namespace ProjectOberon.Emulator;

public sealed class OberonCompilerAddressEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;

    public OberonCompilerAddressEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
    }

    public void EmitAddress(OberonCompilerExpression expression, int destination)
    {
        ArgumentNullException.ThrowIfNull(expression);
        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);

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
                    : OberonCompilerCodeGenerator.StackPointer;
                _generator.EmitImmediate(AluOperation.Add, destination, baseRegister, offset);
                return;
            case OberonCompilerObjectClass.Parameter when !symbol.ReadOnly:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer, offset);
                return;
            case OberonCompilerObjectClass.Parameter
                when symbol.Type?.Form == OberonCompilerTypeForm.Array && symbol.Type.Length < 0:
                _generator.EmitLoad(destination, OberonCompilerCodeGenerator.StackPointer, offset);
                return;
            default:
                throw new NotSupportedException($"'{name.Name}' cannot be passed by reference");
        }
    }
}
