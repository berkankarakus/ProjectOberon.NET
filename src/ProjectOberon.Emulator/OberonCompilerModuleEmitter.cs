using System.Text;

namespace ProjectOberon.Emulator;

public sealed class OberonCompilerModuleEmitter
{
    private readonly OberonCompilerCodeGenerator _generator;
    private readonly OberonCompilerFrameEmitter _frames;
    private readonly OberonCompilerStatementEmitter _statements;
    private readonly Dictionary<OberonCompilerObject, OberonCompilerLabel> _procedureLabels = [];
    private readonly Dictionary<string, int> _stringAddresses = new(StringComparer.Ordinal);
    private readonly List<byte> _moduleData = [];
    private IReadOnlyList<RscCommand> _commands = [];
    private int _variableBytes;
    private int _bodyOffset;
    private int _nextStringOffset;

    public OberonCompilerModuleEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _frames = new OberonCompilerFrameEmitter(generator);
        _statements = new OberonCompilerStatementEmitter(
            generator,
            procedureLabelResolver: ResolveProcedureLabel,
            currentLevel: 1,
            stringAddressResolver: ResolveStringAddress);
    }

    public IReadOnlyList<byte> ModuleData => _moduleData;
    public IReadOnlyList<RscCommand> Commands => _commands;
    public int VariableBytes => _variableBytes;
    public int BodyOffset => _bodyOffset;

    public IReadOnlyList<uint> Emit(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!module.Succeeded) throw new InvalidOperationException("cannot emit an invalid module");

        _procedureLabels.Clear();
        _stringAddresses.Clear();
        _moduleData.Clear();
        _variableBytes = Align(ModuleVariableDataSize(module), 4);
        _nextStringOffset = _variableBytes;
        EnsureModuleData(_nextStringOffset);
        var procedures = module.Procedures ?? [];
        var allProcedures = Flatten(procedures).ToArray();
        foreach (var procedure in allProcedures)
            _procedureLabels[procedure.Symbol] = new OberonCompilerLabel();

        foreach (var procedure in allProcedures)
        {
            _generator.Bind(_procedureLabels[procedure.Symbol]);
            _frames.EmitProcedurePrologue(procedure.FrameSize, procedure.Symbol.Level > 1);
            _frames.SaveParameters(procedure.Symbol);
            var procedureStatements = new OberonCompilerStatementEmitter(
                _generator,
                procedure.FrameSize,
                ResolveProcedureLabel,
                currentLevel: procedure.Symbol.Level + 1,
                stringAddressResolver: ResolveStringAddress);
            if (procedure.Body is not null) procedureStatements.EmitStatementSequence(procedure.Body);
            if (OberonCompilerControlFlowAnalyzer.CanCompleteNormally(procedure.Body))
                _frames.EmitProcedureReturn(procedure.FrameSize);
        }

        _commands = procedures
            .Where(procedure => procedure.Symbol.Exported)
            .Select(procedure => new RscCommand(
                procedure.Symbol.Name,
                checked(GetLabelPosition(procedure.Symbol) * 4)))
            .ToArray();
        _bodyOffset = checked(_generator.ProgramCounter * 4);
        if (module.Body is not null) _statements.EmitStatementSequence(module.Body);
        return _generator.Code;
    }

    public RscModule EmitRsc(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var code = Emit(module);
        var imports = (module.ModuleScope?.Objects ?? [])
            .Where(symbol => symbol.Class == OberonCompilerObjectClass.Module)
            .Select(symbol => new RscImport(symbol.OriginalName ?? symbol.Name, symbol.Key))
            .ToArray();
        var stringPayload = _moduleData.Skip(_variableBytes).ToArray();
        return new RscModule(
            module.ModuleName ?? throw new InvalidOperationException("module name is missing"),
            0,
            1,
            0,
            imports,
            0,
            _variableBytes,
            stringPayload.Length,
            code.Count,
            code,
            _commands,
            [],
            [],
            0,
            0,
            0,
            _bodyOffset,
            (byte)'O')
        {
            StringPayload = stringPayload
        };
    }

    private OberonCompilerLabel? ResolveProcedureLabel(OberonCompilerObject procedure) =>
        _procedureLabels.TryGetValue(procedure, out var label) ? label : null;

    private int GetLabelPosition(OberonCompilerObject procedure)
    {
        if (!_procedureLabels.TryGetValue(procedure, out var label) || label.Position is not int position)
        {
            throw new InvalidOperationException($"procedure label is not bound: {procedure.Name}");
        }

        return position;
    }

    private int ResolveStringAddress(string value)
    {
        if (_stringAddresses.TryGetValue(value, out var address)) return address;

        address = _nextStringOffset;
        var bytes = Encoding.Latin1.GetBytes(value);
        EnsureModuleData(address + bytes.Length + 1);
        for (var index = 0; index < bytes.Length; index++)
            _moduleData[address + index] = bytes[index];
        _moduleData[address + bytes.Length] = 0;
        _stringAddresses.Add(value, address);
        _nextStringOffset = address + bytes.Length + 1;
        return address;
    }

    private static int ModuleVariableDataSize(OberonCompilerModuleParseResult module)
    {
        var end = 0;
        foreach (var symbol in module.ModuleScope?.Objects ?? [])
        {
            if (symbol.Class != OberonCompilerObjectClass.Variable) continue;
            end = Math.Max(end, checked((int)symbol.Value + (symbol.Type?.Size ?? 0)));
        }

        return end;
    }

    private void EnsureModuleData(int size)
    {
        while (_moduleData.Count < size) _moduleData.Add(0);
    }

    private static int Align(int value, int alignment) =>
        checked((value + alignment - 1) / alignment * alignment);

    private static IEnumerable<OberonCompilerProcedureDefinition> Flatten(
        IEnumerable<OberonCompilerProcedureDefinition> procedures)
    {
        foreach (var procedure in procedures)
        {
            yield return procedure;
            foreach (var nested in Flatten(procedure.NestedProcedures)) yield return nested;
        }
    }

}
