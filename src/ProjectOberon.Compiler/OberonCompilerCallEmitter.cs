namespace ProjectOberon.Compiler;

public sealed class OberonCompilerCallEmitter
{
    private readonly IOberonCompilerInstructionWriter _generator;
    private readonly OberonCompilerAddressEmitter _addresses;
    private readonly OberonCompilerRegisterAllocator _registers;
    private readonly Func<OberonCompilerExpression, int, int> _emitExpression;
    private readonly Func<OberonCompilerObject, OberonCompilerLabel?>? _procedureLabelResolver;
    private readonly int _currentLevel;

    public OberonCompilerCallEmitter(
        IOberonCompilerInstructionWriter generator,
        OberonCompilerAddressEmitter addresses,
        OberonCompilerRegisterAllocator registers,
        Func<OberonCompilerExpression, int, int> emitExpression,
        Func<OberonCompilerObject, OberonCompilerLabel?>? procedureLabelResolver,
        int currentLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(registers);
        ArgumentNullException.ThrowIfNull(emitExpression);

        _generator = generator;
        _addresses = addresses;
        _registers = registers;
        _emitExpression = emitExpression;
        _procedureLabelResolver = procedureLabelResolver;
        _currentLevel = currentLevel;
    }

    public void EmitCall(OberonCompilerCallStatement call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var procedure = call.Procedure.Symbol ?? throw new InvalidOperationException(
            $"procedure '{call.Procedure.Name}' is not defined");
        EmitCallArguments(procedure, call.Arguments);
        EmitStaticLink(procedure);
        var target = ResolveProcedureLabel(procedure);
        if (target is null)
        {
            if (procedure.Level == 0 && procedure.Key != 0)
            {
                throw new NotSupportedException(
                    $"external procedure '{call.Procedure.Name}' requires RSC import lowering");
            }

            throw new InvalidOperationException($"procedure '{call.Procedure.Name}' has no code label");
        }
        _generator.EmitCall(target);
    }

    public int EmitFunctionCall(OberonCompilerCallExpression call, int destination)
    {
        ArgumentNullException.ThrowIfNull(call);
        OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination);
        var procedure = call.Procedure.Symbol ?? throw new InvalidOperationException(
            $"function '{call.Procedure.Name}' is not defined");
        if (procedure.Type?.Base?.Form == OberonCompilerTypeForm.None)
            throw new InvalidOperationException($"'{call.Procedure.Name}' is a procedure, not a function");

        EmitCallArguments(procedure, call.Arguments);
        EmitStaticLink(procedure);
        var target = ResolveProcedureLabel(procedure) ?? throw new InvalidOperationException(
            $"function '{call.Procedure.Name}' has no code label");
        _generator.EmitCall(target);
        if (destination != 0)
        {
            _generator.EmitRegister(AluOperation.Mov, destination, 0, 0);
        }
        return destination;
    }

    private void EmitStaticLink(OberonCompilerObject procedure)
    {
        if (procedure.Level <= 1)
        {
            return;
        }

        _generator.EmitRegister(
            AluOperation.Mov,
            OberonCompilerCallingConvention.StaticLinkRegister,
            OberonCompilerCallingConvention.StackPointer,
            OberonCompilerCallingConvention.StackPointer);
        for (var level = _currentLevel; level > procedure.Level; level--)
        {
            _generator.EmitLoad(
                OberonCompilerCallingConvention.StaticLinkRegister,
                OberonCompilerCallingConvention.StaticLinkRegister,
                OberonCompilerCallingConvention.StaticLinkOffset);
        }
    }

    private OberonCompilerLabel? ResolveProcedureLabel(OberonCompilerObject procedure) =>
        _procedureLabelResolver?.Invoke(procedure);

    private void EmitCallArguments(
        OberonCompilerObject procedure,
        IReadOnlyList<OberonCompilerExpression> arguments)
    {
        var parameters = procedure.Type?.Parameters ?? [];
        if (arguments.Count != parameters.Count)
            throw new InvalidOperationException($"procedure '{procedure.Name}' expects {parameters.Count} arguments");

        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var register = parameters
                .Take(index)
                .Sum(ParameterRegisterCount);
            using var preservedArguments = _registers.ReserveRange(0, register);
            if (OberonCompilerTypeRules.IsScalar(parameter.Type))
            {
                if (parameter.ReadOnly)
                {
                    _emitExpression(arguments[index], register);
                }
                else
                {
                    _addresses.EmitAddress(arguments[index], register);
                }
                continue;
            }

            if (parameter.Type?.Form == OberonCompilerTypeForm.Array
                && parameter.Type.Length < 0)
            {
                EmitOpenArrayArgument(arguments[index], register);
                continue;
            }

            if (OberonCompilerTypeRules.IsAggregate(parameter.Type))
            {
                _addresses.EmitAddress(arguments[index], register);
                continue;
            }

            throw new NotSupportedException("record parameter lowering is not implemented");
        }
    }

    private void EmitOpenArrayArgument(OberonCompilerExpression argument, int destination)
    {
        if (argument is OberonCompilerStringLiteralExpression literal)
        {
            _addresses.EmitAddress(argument, destination);
            OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination + 1);
            _generator.EmitMoveImmediate(destination + 1, literal.Value.Length);
            return;
        }

        if (argument is not OberonCompilerNameExpression name || name.Symbol is null)
            throw new NotSupportedException("open array arguments must be addressable names");

        _addresses.EmitAddress(argument, destination);
        var type = name.Symbol.Type;
        if (type?.Form != OberonCompilerTypeForm.Array)
            throw new InvalidOperationException($"'{name.Name}' is not an array");
        if (type.Length >= 0)
        {
            OberonCompilerRegisterAllocator.ValidateExpressionRegister(destination + 1);
            _generator.EmitMoveImmediate(destination + 1, type.Length);
            return;
        }

        if (name.Symbol.Class != OberonCompilerObjectClass.Parameter)
            throw new NotSupportedException("open array length source is not available");
        var lengthBase = _addresses.EmitStorageBase(name.Symbol, destination + 1);
        _generator.EmitLoad(destination + 1, lengthBase,
            checked((int)name.Symbol.Value + 4));
    }

    private static int ParameterRegisterCount(OberonCompilerObject parameter) =>
        parameter.Type?.Form == OberonCompilerTypeForm.Array && parameter.Type.Length < 0 ? 2 : 1;
}
