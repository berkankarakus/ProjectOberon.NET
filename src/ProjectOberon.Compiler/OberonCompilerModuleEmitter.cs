using System.Text;

namespace ProjectOberon.Compiler;

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
    private readonly Dictionary<int, int> _externalImportNumbers = [];
    private bool _emitExternalCalls;
    private int? _firstProgramFixup;
    private int _programFixupHead;

    public OberonCompilerModuleEmitter(OberonCompilerCodeGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generator = generator;
        _frames = new OberonCompilerFrameEmitter(generator);
        _statements = new OberonCompilerStatementEmitter(
            generator,
            frameSize: 0,
            procedureLabelResolver: ResolveProcedureLabel,
            currentLevel: 1,
            stringAddressResolver: ResolveStringAddress,
            externalProcedureEmitter: EmitExternalProcedureCall);
    }

    public IReadOnlyList<byte> ModuleData => _moduleData;
    public IReadOnlyList<RscCommand> Commands => _commands;
    public int VariableBytes => _variableBytes;
    public int BodyOffset => _bodyOffset;

    public IReadOnlyList<uint> Emit(OberonCompilerModuleParseResult module)
    {
        return EmitCore(module, emitExternalCalls: false);
    }

    public RscModule EmitRsc(OberonCompilerModuleParseResult module)
    {
        ArgumentNullException.ThrowIfNull(module);
        EnsureMinimalRscSupported(module);
        var imports = (module.ModuleScope?.Objects ?? [])
            .Where(symbol => symbol.Class == OberonCompilerObjectClass.Module)
            .Select(symbol => new RscImport(symbol.OriginalName ?? symbol.Name, symbol.Key))
            .ToArray();
        ConfigureExternalImports(imports);
        IReadOnlyList<uint> code;
        try
        {
            code = EmitCore(module, emitExternalCalls: true);
        }
        finally
        {
            _emitExternalCalls = false;
            _externalImportNumbers.Clear();
        }

        var stringBytes = Align(_moduleData.Count - _variableBytes, 4);
        EnsureModuleData(_variableBytes + stringBytes);
        var stringPayload = _moduleData.Skip(_variableBytes).Take(stringBytes).ToArray();
        var fixupProgram = 0;
        if (_firstProgramFixup is int firstProgramFixup)
        {
            fixupProgram = firstProgramFixup;
        }

        return new RscModule(
            module.ModuleName ?? throw new InvalidOperationException("module name is missing"),
            0,
            1,
            0,
            imports,
            0,
            _variableBytes,
            stringBytes,
            code.Count,
            code,
            _commands,
            [],
            [],
            fixupProgram,
            0,
            0,
            _bodyOffset,
            (byte)'O')
        {
            StringPayload = stringPayload
        };
    }

    private IReadOnlyList<uint> EmitCore(
        OberonCompilerModuleParseResult module,
        bool emitExternalCalls)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (!module.Succeeded)
        {
            throw new InvalidOperationException("cannot emit an invalid module");
        }

        _emitExternalCalls = emitExternalCalls;
        _firstProgramFixup = null;
        _programFixupHead = 0;
        _generator.Reset();
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
                stringAddressResolver: ResolveStringAddress,
                externalProcedureEmitter: EmitExternalProcedureCall);
            if (procedure.Body is not null)
            {
                procedureStatements.EmitStatementSequence(procedure.Body);
            }
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
        if (module.Body is not null)
        {
            _statements.EmitStatementSequence(module.Body);
        }

        return _generator.Code;
    }

    private void ConfigureExternalImports(IReadOnlyList<RscImport> imports)
    {
        _externalImportNumbers.Clear();
        for (var index = 0; index < imports.Count; index++)
        {
            var importNumber = checked(index + 1);

            if (importNumber > 0x0f)
            {
                throw new NotSupportedException("RSC program fixups support at most 15 imports");
            }

            if (!_externalImportNumbers.TryAdd(imports[index].Key, importNumber))
            {
                throw new InvalidOperationException(
                    $"duplicate external module key: {imports[index].Key}");
            }
        }
    }

    private void EmitExternalProcedureCall(OberonCompilerObject procedure)
    {
        if (!_emitExternalCalls)
        {
            throw new NotSupportedException(
                $"external procedure '{procedure.Name}' requires RSC import lowering");
        }

        if (!_externalImportNumbers.TryGetValue(procedure.Key, out var importNumber))
        {
            throw new InvalidOperationException(
                $"external module key is not imported: {procedure.Key}");
        }

        if (procedure.Value is < 0 or > 0xff)
        {
            throw new NotSupportedException(
                $"external procedure entry is outside the RSC range: {procedure.Name}");
        }

        var fixupPosition = _generator.ProgramCounter;

        if (fixupPosition == 0)
        {
            _generator.EmitRegister(AluOperation.Mov, 0, 0, 0);
            fixupPosition = _generator.ProgramCounter;
        }

        var previousDistance = fixupPosition - _programFixupHead;
        if (previousDistance is < 1 or > 0xfff)
        {
            throw new InvalidOperationException("RSC program fixup chain distance is out of range");
        }

        var entryNumber = checked((int)procedure.Value);
        var encodedImmediate = ((entryNumber & 0x0f) << 12) | previousDistance;
        _generator.EmitImmediate(
            (AluOperation)(entryNumber >> 4),
            0,
            importNumber,
            encodedImmediate);

        if (_firstProgramFixup is null)
        {
            _firstProgramFixup = fixupPosition;
        }

        _programFixupHead = fixupPosition;
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
        if (_stringAddresses.TryGetValue(value, out var address))
        {
            return address;
        }

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

    private static void EnsureMinimalRscSupported(OberonCompilerModuleParseResult module)
    {
        foreach (var symbol in module.ModuleScope?.Objects ?? [])
        {
            if (symbol.Type is not null && RequiresTypeDescriptor(symbol.Type, []))
            {
                throw new NotSupportedException(
                    $"minimal RSC output does not support type descriptors: {symbol.Name}");
            }
        }
    }

    private static bool RequiresTypeDescriptor(
        OberonCompilerType type,
        HashSet<OberonCompilerType> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }
        return type.Form switch
        {
            OberonCompilerTypeForm.Pointer or OberonCompilerTypeForm.Record => true,
            OberonCompilerTypeForm.Array when type.Base is not null =>
                RequiresTypeDescriptor(type.Base, visited),
            _ => false
        };
    }

    private static int ModuleVariableDataSize(OberonCompilerModuleParseResult module)
    {
        var end = 0;
        foreach (var symbol in module.ModuleScope?.Objects ?? [])
        {
            if (symbol.Class != OberonCompilerObjectClass.Variable)
            {
                continue;
            }
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
            foreach (var nested in Flatten(procedure.NestedProcedures))
            {
                yield return nested;
            }
        }
    }

}
