using ProjectOberon.Emulator;

internal static class OberonCompilerRscTests
{
    public static void Run()
    {
        EmitsMinimalRscModule();
        EmitsStringPayload();
        EmitsImportsAndExportedCommands();
        EmitsExternalProcedureFixup();
        EmitsExternalModuleDeterministically();
        EmitsTypeDescriptorsDeterministically();
        RoundTripsAggregateDescriptorMetadata();
        ReportsTypeDescriptorExtensionLimit();
        ExternalEmissionDoesNotAffectLocalEmission();
        ExecutesExternalProcedureCall();
        ExecutesExternalVarParameterCall();
        ExecutesExternalRecordParameterCall();
        ExecutesExternalFixedArrayParameterCall();
        EmitsExternalFunctionFixup();
        EmitsExternalRealFunctionExpression();
        EmitsExternalValueParameterFixup();
        EmitsMultipleExternalFixups();
        AcceptsMaximumExternalImportCount();
        EmitsExternalVariableDataFixup();
        EmitsExternalVariableStoreFixup();
        EmitsExternalByteVariableStoreFixup();
        EmitsMultipleExternalDataFixups();
        EmitsExternalRecordVariableDataFixup();
        EmitsExternalRecordVariableReadFixup();
        EmitsExternalPointerVariableDataFixup();
        EmitsExternalArrayVariableDataFixup();
        EmitsExternalVarParameterCall();
        EmitsExternalFixedArrayParameterCall();
        EmitsExternalRealOpenArrayParameterCall();
        EmitsExternalPointerParameterCall();
        EmitsExternalRecordParameterCall();
        EmitsExternalVarRecordParameterCall();
        EmitsExternalOpenArrayRecordParameterCall();
        RejectsInvalidExternalImportTables();
        LoadsCompilerProducedRsc();
        ExecutesCompilerProducedBody();
        ExecutesCompilerProducedRecordCopy();
        ExecutesCompilerProducedAggregateFieldCopy();
        ExecutesCompilerProducedAggregateFieldStore();
        ExecutesCompilerProducedArrayElementLoad();
        ExecutesCompilerProducedArrayElementStore();
        ExecutesCompilerProducedPointerDereferenceLoad();
        EmitsNestedPointerArraySelectors();
        ExecutesCompilerProducedArrayCopy();
        ExecutesCompilerProducedByteArrayCopy();
        ExecutesCompilerProducedFiveByteArrayCopy();
        ExecutesCompilerProducedSmallByteArrayCopies();
        ExecutesCompilerProducedStringBody();
        ExecutesCompilerProducedGlobalAssignmentAfterDescriptor();
        ExecutesExactTypeTestAsTrue();
        EmitsImportedTypeTestDescriptorLookup();
        ExecutesImportedTypeTestAgainstRuntimeTags();
        EmitsRecordExtensionTypeTestLookup();
        EmitsRecursivePointerFieldRootReference();
        EmitsRecursivePointerArrayRootReferences();
        EmitsRecordTypeDescriptor();
        EmitsPointerTypeDescriptorAndRootReferences();
        EmitsRecordExtensionTypeFixup();
        EmitsNestedRecordTypeDescriptor();
        EmitsRecordDescriptorAllocationBoundaries();
        EmitsRecursiveRecordDescriptorsDeterministically();
        EmitsImportedRecordBaseDescriptor();
        ExecutesImportedRecordBaseDescriptor();
        EmitsImportedRecordExtensionChain();
    }

    private static void EmitsMinimalRscModule()
    {
        const string source = "MODULE Demo; VAR value: INTEGER; BEGIN value := 42 END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal("Demo", module.Name);
        TestAssertions.Equal(4, module.VariableBytes);
        TestAssertions.Equal(module.Code.Count, module.CodeWords);
        TestAssertions.Equal(0, module.TypeDescriptorBytes);
        TestAssertions.Equal(0, module.StringBytes);
        TestAssertions.Equal((byte)'O', module.Trailer);

        var parsed = RscModuleReader.Parse(RscModuleWriter.Write(module));
        TestAssertions.Equal(module.Name, parsed.Name);
        TestAssertions.True(module.Code.SequenceEqual(parsed.Code));
        TestAssertions.Equal(module.BodyOffset, parsed.BodyOffset);
    }

    private static void EmitsImportsAndExportedCommands()
    {
        const string source = "MODULE Demo; IMPORT Files; PROCEDURE Start*; END Start; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.Imports.Count);
        TestAssertions.Equal("Files", module.Imports[0].Name);
        TestAssertions.Equal(1, module.Commands.Count);
        TestAssertions.Equal("Start", module.Commands[0].Name);
        TestAssertions.Equal(0, module.Commands[0].CodeOffset);
        TestAssertions.True(module.BodyOffset > module.Commands[0].CodeOffset);
    }

    private static void EmitsExternalProcedureFixup()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 7) { IsProcedure = true }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.Imports.Count);
        TestAssertions.Equal(1, module.FixupProgram);
        var placeholder = module.Code[module.FixupProgram];
        TestAssertions.Equal(1u, (placeholder >> 20) & 0x0f);
        TestAssertions.Equal(7u, (placeholder >> 12) & 0xff);
        TestAssertions.Equal(1u, placeholder & 0xfff);

        var roundTrip = RscModuleReader.Parse(RscModuleWriter.Write(module));
        TestAssertions.Equal(module.FixupProgram, roundTrip.FixupProgram);
        TestAssertions.True(module.Code.SequenceEqual(roundTrip.Code));
    }

    private static void EmitsExternalModuleDeterministically()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 7) { IsProcedure = true }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var emitter = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator());

        var first = emitter.EmitRsc(result);
        var second = emitter.EmitRsc(result);

        TestAssertions.True(first.Code.SequenceEqual(second.Code));
        TestAssertions.Equal(first.FixupProgram, second.FixupProgram);
        TestAssertions.Equal(first.FixupData, second.FixupData);
        TestAssertions.Equal(first.VariableBytes, second.VariableBytes);
        TestAssertions.Equal(first.BodyOffset, second.BodyOffset);
    }

    private static void EmitsTypeDescriptorsDeterministically()
    {
        const string source = "MODULE Types; TYPE Base = RECORD value: INTEGER; END; Extended = RECORD (Base) flag: BOOLEAN; END; Ref = POINTER TO Extended; VAR first: Ref; second: Extended; BEGIN END Types.";
        var result = new OberonCompilerParser(source).ParseModule();
        var emitter = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator());

        var first = emitter.EmitRsc(result);
        var second = emitter.EmitRsc(result);

        TestAssertions.True(first.TypeDescriptorPayload.SequenceEqual(second.TypeDescriptorPayload));
        TestAssertions.True(first.PointerReferences.SequenceEqual(second.PointerReferences));
        TestAssertions.Equal(first.FixupTypes, second.FixupTypes);
    }

    private static void RoundTripsAggregateDescriptorMetadata()
    {
        const string source = "MODULE Types; TYPE Node = RECORD next: POINTER TO Node; value: INTEGER; END; VAR node: Node; BEGIN END Types.";
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator())
            .EmitRsc(new OberonCompilerParser(source).ParseModule());
        var roundTrip = RscModuleReader.Parse(RscModuleWriter.Write(definition));

        TestAssertions.Equal(definition.TypeDescriptorBytes, roundTrip.TypeDescriptorBytes);
        TestAssertions.True(definition.TypeDescriptorPayload.SequenceEqual(roundTrip.TypeDescriptorPayload));
        TestAssertions.True(definition.PointerReferences.SequenceEqual(roundTrip.PointerReferences));
        TestAssertions.True(definition.Code.SequenceEqual(roundTrip.Code));
    }

    private static void ReportsTypeDescriptorExtensionLimit()
    {
        const string source = "MODULE Types; TYPE A = RECORD value: INTEGER; END; B = RECORD (A) value: INTEGER; END; C = RECORD (B) value: INTEGER; END; D = RECORD (C) value: INTEGER; END; E = RECORD (D) value: INTEGER; END; VAR item: E; BEGIN END Types.";
        var result = new OberonCompilerParser(source).ParseModule();

        var error = TestAssertions.Throws<NotSupportedException>(() =>
            new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result));

        TestAssertions.True(error.Message.Contains("at most 3 record extensions", StringComparison.Ordinal));
    }

    private static void ExternalEmissionDoesNotAffectLocalEmission()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 7) { IsProcedure = true }]);
        var externalResult = new OberonCompilerParser(
            "MODULE External; IMPORT IO; BEGIN IO.Run; END External.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var localResult = new OberonCompilerParser(
            "MODULE Local; PROCEDURE Run; BEGIN END Run; BEGIN Run; END Local.")
            .ParseModule();
        var emitter = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator());

        emitter.EmitRsc(externalResult);
        var localCode = emitter.Emit(localResult);

        TestAssertions.True(localCode.Count > 0);
        TestAssertions.True(localCode
            .Select(Risc5Disassembler.Format)
            .Any(instruction => instruction.StartsWith("BTL ", StringComparison.Ordinal)));
    }

    private static void ExecutesExternalProcedureCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 0) { IsProcedure = true }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(symbols);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        var called = false;
        var initialProgramCounter = cpu.PcByte;
        var dispatcher = new OberonHostCallDispatcher();
        runtime.BindExternal(
            new OberonExternalModuleBinding("IO", 0x3000, 0x4000, [0])
            {
                EntryHandlers =
                [
                    context =>
                    {
                        called = true;
                        context.Cpu.SetProgramCounter(Memory.RomBase);
                    }
                ]
            },
            dispatcher);

        var image = runtime.MemoryLoader.Load("Demo");
        runtime.Initialize(image, dispatcher);

        TestAssertions.True(called);
        TestAssertions.Equal(initialProgramCounter, cpu.PcByte);
    }

    private static void ExecutesExternalVarParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [false],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Integer]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; VAR value: INTEGER; BEGIN IO.Set(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(symbols);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        uint? receivedAddress = null;
        var dispatcher = new OberonHostCallDispatcher();
        runtime.BindExternal(
            new OberonExternalModuleBinding("IO", 0x3000, 0x4000, [0])
            {
                EntryHandlers =
                [
                    context =>
                    {
                        receivedAddress = context.Cpu.Register(0);
                        context.Cpu.SetProgramCounter(Memory.RomBase);
                    }
                ]
            },
            dispatcher);

        var image = runtime.MemoryLoader.Load("Demo");
        runtime.Initialize(image, dispatcher);

        TestAssertions.Equal(image.DataAddress, receivedAddress);
    }

    private static void ExecutesExternalRecordParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [4],
                ParameterRecordFields =
                [
                    [new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)]
                ]
            }]);
        var types = CreateRecordTypeModule();
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO, Types; PROCEDURE Run; VAR value: Types.Node; BEGIN IO.Set(value); END Run; BEGIN Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["IO"] = symbols,
                ["Types"] = types
            })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(symbols);
        loader.RegisterExternal(types);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        uint? receivedAddress = null;
        var dispatcher = new OberonHostCallDispatcher();
        runtime.BindExternal(new OberonExternalModuleBinding("Types", 0x5000, 0x6000, [0]));
        runtime.BindExternal(
            new OberonExternalModuleBinding("IO", 0x3000, 0x4000, [0])
            {
                EntryHandlers =
                [
                    context =>
                    {
                        receivedAddress = context.Cpu.Register(0);
                        context.Cpu.SetProgramCounter(Memory.RomBase);
                    }
                ]
            },
            dispatcher);

        var image = runtime.Start("Demo", 0xf000);
        memory.Write32(image.DataAddress, 0x1234_5678);
        runtime.Initialize(image, dispatcher);

        TestAssertions.True(receivedAddress > image.DataAddress);
        TestAssertions.True(receivedAddress < (uint)memory.RamSize);
    }

    private static void ExecutesExternalFixedArrayParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Consume", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = new int?[] { 3 },
                ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Integer]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Values = ARRAY 3 OF INTEGER; VAR value: Values; BEGIN IO.Consume(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(symbols);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        uint? receivedAddress = null;
        var dispatcher = new OberonHostCallDispatcher();
        runtime.BindExternal(
            new OberonExternalModuleBinding("IO", 0x3000, 0x4000, [0])
            {
                EntryHandlers =
                [
                    context =>
                    {
                        receivedAddress = context.Cpu.Register(0);
                        context.Cpu.SetProgramCounter(Memory.RomBase);
                    }
                ]
            },
            dispatcher);

        var image = runtime.MemoryLoader.Load("Demo");
        runtime.Initialize(image, dispatcher);

        TestAssertions.Equal(image.DataAddress, receivedAddress);
    }

    private static void EmitsExternalFunctionFixup()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Value", 1, 0)
            {
                IsProcedure = true,
                HasResult = true
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; VAR result: INTEGER; BEGIN result := IO.Value(); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);

        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.FixupProgram);
        TestAssertions.Equal(1u, (module.Code[module.FixupProgram] >> 20) & 0x0f);
    }

    private static void EmitsExternalRealFunctionExpression()
    {
        var symbols = new OberonSymbolModule(
            "Math",
            0x1234,
            1,
            [new OberonSymbolExport("Measure", 1, 0)
            {
                IsProcedure = true,
                HasResult = true,
                ResultTypeForm = (int)OberonCompilerTypeForm.Real
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Math; VAR value: REAL; BEGIN value := Math.Measure() + 1.0; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Math"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);

        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        TestAssertions.True(module.Code.Any(
            word => Risc5Disassembler.Format(word).StartsWith("FAD ", StringComparison.Ordinal)));
    }

    private static void EmitsExternalValueParameterFixup()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Run", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; BEGIN IO.Run(42); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);

        TestAssertions.Equal("MOV r0, 0x002a", Risc5Disassembler.Format(generator.Code[0]));
        TestAssertions.Equal(1, module.FixupProgram);
    }

    private static void EmitsMultipleExternalFixups()
    {
        var firstSymbols = new OberonSymbolModule(
            "First",
            0x1001,
            1,
            [new OberonSymbolExport("Run", 1, 0) { IsProcedure = true }]);
        var secondSymbols = new OberonSymbolModule(
            "Second",
            0x1002,
            1,
            [new OberonSymbolExport("Run", 1, 0) { IsProcedure = true }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT First, Second; BEGIN First.Run; Second.Run; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["First"] = firstSymbols,
                ["Second"] = secondSymbols
            })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(2, module.Imports.Count);
        TestAssertions.Equal(1, module.FixupProgram);
        TestAssertions.Equal(1u, (module.Code[1] >> 20) & 0x0f);
        TestAssertions.Equal(2u, (module.Code[2] >> 20) & 0x0f);
        TestAssertions.Equal(1u, module.Code[1] & 0xfff);
        TestAssertions.Equal(1u, module.Code[2] & 0xfff);
    }

    private static void AcceptsMaximumExternalImportCount()
    {
        var importNames = string.Join(", ", Enumerable.Range(0, 15).Select(index => $"M{index}"));
        var imports = Enumerable.Range(0, 15)
            .ToDictionary(
                index => $"M{index}",
                index => new OberonSymbolModule($"M{index}", 0x2000 + index, 1, []));
        var result = new OberonCompilerParser(
            $"MODULE Demo; IMPORT {importNames}; BEGIN END Demo.",
            importedModules: imports)
            .ParseModule();

        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(15, module.Imports.Count);
    }

    private static void RejectsInvalidExternalImportTables()
    {
        var tooManyImportNames = string.Join(
            ", ",
            Enumerable.Range(0, 16).Select(index => $"M{index}"));
        var tooManyImports = Enumerable.Range(0, 16)
            .ToDictionary(
                index => $"M{index}",
                index => new OberonSymbolModule(
                    $"M{index}",
                    0x2000 + index,
                    1,
                    []));
        var tooManyResult = new OberonCompilerParser(
            $"MODULE Demo; IMPORT {tooManyImportNames}; BEGIN END Demo.",
            importedModules: tooManyImports)
            .ParseModule();

        TestAssertions.True(tooManyResult.Succeeded);
        TestAssertions.Throws<NotSupportedException>(() =>
            new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(tooManyResult));

        var duplicateResult = new OberonCompilerParser(
            "MODULE Demo; IMPORT First, Second; BEGIN END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["First"] = new OberonSymbolModule("First", 0x3000, 1, []),
                ["Second"] = new OberonSymbolModule("Second", 0x3000, 1, [])
            })
            .ParseModule();

        TestAssertions.True(duplicateResult.Succeeded);
        TestAssertions.Throws<InvalidOperationException>(() =>
            new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(duplicateResult));
    }

    private static void EmitsExternalVariableDataFixup()
    {
        var symbols = new OberonSymbolModule(
            "Config",
            0x1234,
            1,
            [new OberonSymbolExport("Value", 2, 12)]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Config; VAR result: INTEGER; BEGIN result := Config.Value; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Config"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.FixupData);
        TestAssertions.Equal(1u, (module.Code[1] >> 20) & 0x0f);
        TestAssertions.Equal(1u, module.Code[1] & 0xfff);
        TestAssertions.Equal(12u, module.Code[2] & 0xff);

        var bytes = RscModuleWriter.Write(module);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(symbols);
        var memory = new Memory(64 * 1024);
        var runtime = new OberonModuleRuntime(memory, new Risc5Cpu(memory), loader, 0x1000, 0x100);
        runtime.BindExternal(
            new OberonExternalModuleBinding(
                "Config",
                0x3000,
                0x4000,
                Enumerable.Range(0, 13).Select(index => index == 12 ? 0x30u : 0u).ToArray()));

        var image = runtime.MemoryLoader.Load("Demo");
        TestAssertions.Equal(0x30u, memory.Read32(image.CodeAddress + 8) & 0xffff);
    }

    private static void EmitsMultipleExternalDataFixups()
    {
        var first = new OberonSymbolModule(
            "First",
            0x1001,
            1,
            [new OberonSymbolExport("Value", 2, 0)]);
        var second = new OberonSymbolModule(
            "Second",
            0x1002,
            1,
            [new OberonSymbolExport("Value", 2, 4)]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT First, Second; VAR result: INTEGER; BEGIN result := First.Value + Second.Value; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["First"] = first,
                ["Second"] = second
            })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(2, module.Imports.Count);
        TestAssertions.True(module.FixupData > 0);
    }

    private static void EmitsExternalVariableStoreFixup()
    {
        var symbols = new OberonSymbolModule(
            "Config",
            0x1234,
            1,
            [new OberonSymbolExport("Value", 2, 12)]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Config; BEGIN Config.Value := 7; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Config"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.FixupData);
        TestAssertions.Equal(1u, (module.Code[1] >> 20) & 0x0f);
        TestAssertions.Equal(1u, module.Code[1] & 0xfff);
        TestAssertions.Equal(12u, module.Code[2] & 0xff);
        TestAssertions.True(Risc5Disassembler.Format(module.Code[3]).StartsWith("STR r0"));
    }

    private static void EmitsExternalByteVariableStoreFixup()
    {
        var symbols = new OberonSymbolModule(
            "Config",
            0x1234,
            1,
            [new OberonSymbolExport("Flag", 2, 12)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Boolean,
                TypeSize = 1
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Config; BEGIN Config.Flag := TRUE; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Config"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, module.FixupData);
        TestAssertions.True(Risc5Disassembler.Format(module.Code[3]).StartsWith("STR.B r0"));
    }

    private static void EmitsExternalRecordVariableDataFixup()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Current", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; BEGIN State.Current.value := State.Current.value; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.True(module.FixupData > 0);
    }

    private static void EmitsExternalRecordVariableReadFixup()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Current", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; VAR result: INTEGER; BEGIN result := State.Current.value; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(1, definition.FixupData);
        TestAssertions.True(definition.Code.Select(Risc5Disassembler.Format)
            .Count(instruction => instruction.StartsWith("LDR r0")) >= 2);
    }

    private static void EmitsExternalPointerVariableDataFixup()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Current", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Pointer,
                BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                BaseTypeSize = 4,
                BaseRecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; BEGIN State.Current^.value := 1; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        TestAssertions.True(module.FixupData > 0);
    }

    private static void EmitsExternalArrayVariableDataFixup()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Values", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Array,
                ArrayLength = 2,
                BaseTypeForm = (int)OberonCompilerTypeForm.Integer,
                BaseTypeSize = 4,
                TypeSize = 8
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; BEGIN State.Values[0] := State.Values[1]; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        TestAssertions.True(module.FixupData > 0);
    }

    private static void EmitsExternalVarParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = new int?[] { -1 }
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Values = ARRAY 2 OF INTEGER; VAR value: Values; BEGIN IO.Set(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);

        TestAssertions.True(generator.Code
            .Select(Risc5Disassembler.Format)
            .Contains("ADD r0, r13, 0x0000"));
        TestAssertions.True(generator.Code
            .Select(Risc5Disassembler.Format)
            .Contains("MOV r1, 0x0002"));
        TestAssertions.True(module.FixupProgram > 0);
    }

    private static void EmitsExternalFixedArrayParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = new int?[] { 3 }
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Values = ARRAY 3 OF INTEGER; VAR value: Values; BEGIN IO.Set(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);

        TestAssertions.True(generator.Code
            .Select(Risc5Disassembler.Format)
            .Contains("ADD r0, r13, 0x0000"));
        TestAssertions.True(module.FixupProgram > 0);
    }

    private static void EmitsExternalRealOpenArrayParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "Math",
            0x1234,
            1,
            [new OberonSymbolExport("Consume", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = new int?[] { -1 },
                ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Real]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Math; TYPE Values = ARRAY 2 OF REAL; VAR value: Values; BEGIN Math.Consume(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Math"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var imported = result.ModuleScope!.Objects.Single(obj => obj.Name == "Math");
        var parameter = imported.FindMember("Consume")!.Type!.Parameters[0];
        TestAssertions.Equal(OberonCompilerTypeForm.Array, parameter.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Real, parameter.Type.Base!.Form);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Contains("MOV r1, 0x0002"));
        TestAssertions.True(module.FixupProgram > 0);
    }

    private static void EmitsExternalPointerParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Pointer],
                ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [0]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO; TYPE Node = RECORD END; Ptr = POINTER TO Node; VAR value: Ptr; BEGIN IO.Set(value); END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["IO"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var importedModule = result.ModuleScope!.Objects.Single(obj => obj.Name == "IO");
        var parameter = importedModule.FindMember("Set")!.Type!.Parameters[0];

        TestAssertions.Equal(OberonCompilerTypeForm.Pointer, parameter.Type!.Form);
        TestAssertions.Equal(OberonCompilerTypeForm.Record, parameter.Type.Base!.Form);
        TestAssertions.Equal(0, parameter.Type.Base.Size);
    }

    private static void EmitsExternalRecordParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [4],
                ParameterRecordFields =
                [
                    [new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)]
                ]
            }]);
        var types = new OberonSymbolModule(
            "Types",
            0x4321,
            1,
            [new OberonSymbolExport("Node", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO, Types; PROCEDURE Relay(value: Types.Node); BEGIN IO.Set(value); END Relay; BEGIN END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["IO"] = symbols,
                ["Types"] = types
            })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);

        var instructions = generator.Code.Select(Risc5Disassembler.Format).ToArray();
        TestAssertions.True(instructions.Contains("LDR r0, [r14, 4]"));
        TestAssertions.True(instructions.Contains("MOV r0, 0x0004"));
        TestAssertions.True(module.FixupProgram > 0);
    }

    private static void EmitsExternalVarRecordParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Set", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [false],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [4],
                ParameterRecordFields =
                [
                    [new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)]
                ]
            }]);
        var types = new OberonSymbolModule(
            "Types",
            0x4321,
            1,
            [new OberonSymbolExport("Node", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO, Types; PROCEDURE Relay(VAR value: Types.Node); BEGIN IO.Set(value); END Relay; BEGIN END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["IO"] = symbols,
                ["Types"] = types
            })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);
        TestAssertions.True(module.FixupProgram > 0);
    }

    private static void EmitsExternalOpenArrayRecordParameterCall()
    {
        var symbols = new OberonSymbolModule(
            "IO",
            0x1234,
            1,
            [new OberonSymbolExport("Consume", 1, 0)
            {
                IsProcedure = true,
                ParameterCount = 1,
                ParameterReadOnly = [true],
                ParameterTypeForms = [(int)OberonCompilerTypeForm.Array],
                ParameterArrayLengths = [-1],
                ParameterBaseTypeForms = [(int)OberonCompilerTypeForm.Record],
                ParameterBaseTypeSizes = [4],
                ParameterBaseRecordFields =
                [
                    [new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)]
                ]
            }]);
        var types = new OberonSymbolModule(
            "Types",
            0x4321,
            1,
            [new OberonSymbolExport("Node", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT IO, Types; PROCEDURE Relay(values: ARRAY OF Types.Node); BEGIN IO.Consume(values); END Relay; BEGIN END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule>
            {
                ["IO"] = symbols,
                ["Types"] = types
            })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var generator = new OberonCompilerCodeGenerator();
        var module = new OberonCompilerModuleEmitter(generator).EmitRsc(result);
        TestAssertions.True(module.FixupProgram > 0);
        TestAssertions.True(generator.Code
            .Select(Risc5Disassembler.Format)
            .Any(instruction => instruction.StartsWith("LDR r0")));
    }

    private static void EmitsStringPayload()
    {
        const string source = "MODULE Strings; VAR text: ARRAY 6 OF CHAR; BEGIN text := \"hello\"; END Strings.";
        var result = new OberonCompilerParser(source).ParseModule();
        var module = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(8, module.VariableBytes);
        TestAssertions.Equal(8, module.StringBytes);
        TestAssertions.True(module.StringPayload.SequenceEqual(
            new byte[] { (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0, 0, 0 }));

        var parsed = RscModuleReader.Parse(RscModuleWriter.Write(module));
        TestAssertions.True(module.StringPayload.SequenceEqual(parsed.StringPayload));
    }

    private static void LoadsCompilerProducedRsc()
    {
        const string source = "MODULE Demo; PROCEDURE Start*; END Start; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.StartCommand("Demo", "Start");

        TestAssertions.Equal(image.CodeAddress, cpu.PcByte);
        TestAssertions.Equal(image.CodeAddress, image.FindCommandAddress("Start")!.Value);
        TestAssertions.Equal(definition.BodyOffset, checked((int)(image.BodyAddress - image.CodeAddress)));
    }

    private static void ExecutesCompilerProducedBody()
    {
        const string source = "MODULE Demo; VAR value: INTEGER; BEGIN value := 42 END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Demo");
        for (var step = 0; step < 4 && memory.Read32(image.DataAddress) != 42; step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(42u, memory.Read32(image.DataAddress));
        TestAssertions.Equal(image.DataAddress, cpu.Register(13));
    }

    private static void ExecutesCompilerProducedRecordCopy()
    {
        var types = CreateRecordTypeModule();
        const string source = "MODULE Records; IMPORT Types; VAR result: INTEGER; PROCEDURE Run; VAR source, target: Types.Node; BEGIN source.value := 305419896; target := source; result := target.value; END Run; BEGIN Run; END Records.";
        var result = new OberonCompilerParser(
            source,
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = types })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Records" ? bytes : null);
        loader.RegisterExternal(types);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        runtime.BindExternal(new OberonExternalModuleBinding("Types", 0x5000, 0x6000, [0]));
        var image = runtime.Start("Records", 0xf000);
        for (var step = 0; step < 32 && memory.Read32(image.DataAddress) != 305419896; step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(305419896u, memory.Read32(image.DataAddress));
    }

    private static void ExecutesCompilerProducedAggregateFieldCopy()
    {
        const string source = "MODULE Records; TYPE Holder = RECORD values: ARRAY 2 OF INTEGER; END; VAR holder: Holder; target: ARRAY 2 OF INTEGER; BEGIN target := holder.values; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Records" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Records");
        var variableDataAddress = image.DataAddress + (uint)definition.TypeDescriptorBytes;
        memory.Write32(variableDataAddress, 11);
        memory.Write32(variableDataAddress + 4, 22);
        for (var step = 0; step < 32
            && (memory.Read32(variableDataAddress + 8) != 11
                || memory.Read32(variableDataAddress + 12) != 22); step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(11u, memory.Read32(variableDataAddress + 8));
        TestAssertions.Equal(22u, memory.Read32(variableDataAddress + 12));
    }

    private static void ExecutesCompilerProducedAggregateFieldStore()
    {
        const string source = "MODULE Records; TYPE Holder = RECORD values: ARRAY 2 OF INTEGER; END; VAR holder: Holder; BEGIN holder.values[0] := 11; holder.values[1] := 22; END Records.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Records" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Records");
        var variableDataAddress = image.DataAddress + (uint)definition.TypeDescriptorBytes;
        for (var step = 0; step < 32
            && (memory.Read32(variableDataAddress) != 11
                || memory.Read32(variableDataAddress + 4) != 22); step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(11u, memory.Read32(variableDataAddress));
        TestAssertions.Equal(22u, memory.Read32(variableDataAddress + 4));
    }

    private static void ExecutesCompilerProducedArrayElementLoad()
    {
        const string source = "MODULE Arrays; VAR values: ARRAY 2 OF INTEGER; result: INTEGER; BEGIN result := values[1]; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Arrays" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Arrays");
        memory.Write32(image.DataAddress, 11);
        memory.Write32(image.DataAddress + 4, 22);
        for (var step = 0; step < 24 && memory.Read32(image.DataAddress + 8) != 22; step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(22u, memory.Read32(image.DataAddress + 8));
    }

    private static void ExecutesCompilerProducedArrayElementStore()
    {
        const string source = "MODULE Arrays; VAR values: ARRAY 2 OF INTEGER; BEGIN values[0] := 11; values[1] := 22; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Arrays" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Arrays");
        for (var step = 0; step < 24
            && (memory.Read32(image.DataAddress) != 11
                || memory.Read32(image.DataAddress + 4) != 22); step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(11u, memory.Read32(image.DataAddress));
        TestAssertions.Equal(22u, memory.Read32(image.DataAddress + 4));
    }

    private static void ExecutesCompilerProducedPointerDereferenceLoad()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Current", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Pointer,
                BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                BaseTypeSize = 4,
                BaseRecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; VAR result: INTEGER; BEGIN result := State.Current^.value; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var instructions = definition.Code.Select(Risc5Disassembler.Format).ToArray();

        TestAssertions.True(instructions.Count(instruction => instruction.StartsWith("LDR r0")) >= 2);
    }

    private static void EmitsNestedPointerArraySelectors()
    {
        var symbols = new OberonSymbolModule(
            "State",
            0x1234,
            1,
            [new OberonSymbolExport("Current", 2, 0)
            {
                IsProcedure = false,
                TypeForm = (int)OberonCompilerTypeForm.Pointer,
                BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                BaseTypeSize = 8,
                BaseRecordFields =
                [
                    new OberonSymbolField(
                        "values",
                        0,
                        (int)OberonCompilerTypeForm.Array,
                        8,
                        2,
                        (int)OberonCompilerTypeForm.Integer,
                        4)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT State; VAR result: INTEGER; BEGIN result := State.Current^.values[1]; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["State"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var instructions = definition.Code.Select(Risc5Disassembler.Format).ToArray();

        TestAssertions.True(instructions.Any(instruction => instruction.StartsWith("LSL ")));
        TestAssertions.False(instructions.Any(instruction => instruction.StartsWith("MUL ")));
        TestAssertions.True(instructions.Count(instruction => instruction.StartsWith("LDR r0")) >= 2);
    }

    private static void ExecutesCompilerProducedArrayCopy()
    {
        const string source = "MODULE Arrays; TYPE Values = ARRAY 2 OF INTEGER; VAR source, target: Values; BEGIN target := source; END Arrays.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Arrays" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Arrays");
        memory.Write32(image.DataAddress, 11);
        memory.Write32(image.DataAddress + 4, 22);
        for (var step = 0; step < 64
            && (memory.Read32(image.DataAddress + 8) != 11
                || memory.Read32(image.DataAddress + 12) != 22); step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(11u, memory.Read32(image.DataAddress + 8));
        TestAssertions.Equal(22u, memory.Read32(image.DataAddress + 12));
    }

    private static void ExecutesCompilerProducedByteArrayCopy()
    {
        const string source = "MODULE Bytes; TYPE Values = ARRAY 3 OF CHAR; VAR source, target: Values; BEGIN target := source; END Bytes.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Bytes" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Bytes");
        memory.Write8(image.DataAddress, (byte)'O');
        memory.Write8(image.DataAddress + 1, (byte)'K');
        memory.Write8(image.DataAddress + 2, (byte)'!');
        for (var step = 0; step < 64
            && (memory.Read8(image.DataAddress + 4) != (byte)'O'
                || memory.Read8(image.DataAddress + 5) != (byte)'K'
                || memory.Read8(image.DataAddress + 6) != (byte)'!'); step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal((uint)'O', memory.Read8(image.DataAddress + 4));
        TestAssertions.Equal((uint)'K', memory.Read8(image.DataAddress + 5));
        TestAssertions.Equal((uint)'!', memory.Read8(image.DataAddress + 6));
    }

    private static OberonSymbolModule CreateRecordTypeModule()
    {
        return new OberonSymbolModule(
            "Types",
            0x4321,
            1,
            [new OberonSymbolExport("Node", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
    }

    private static OberonSymbolModule CreateRecordHierarchyTypeModule()
    {
        var baseFields = new[]
        {
            new OberonSymbolField(
                "value",
                0,
                (int)OberonCompilerTypeForm.Integer,
                4,
                null,
                null,
                null)
        };
        return new OberonSymbolModule(
            "Types",
            0x4321,
            1,
            [
                new OberonSymbolExport("Node", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 4,
                    RecordFields = baseFields
                },
                new OberonSymbolExport("Derived", 5, 0)
                {
                    TypeForm = (int)OberonCompilerTypeForm.Record,
                    TypeSize = 8,
                    BaseTypeForm = (int)OberonCompilerTypeForm.Record,
                    BaseTypeSize = 4,
                    BaseRecordFields = baseFields,
                    RecordFields =
                    [
                        new OberonSymbolField(
                            "extra",
                            4,
                            (int)OberonCompilerTypeForm.Integer,
                            4,
                            null,
                            null,
                            null)
                    ]
                }
            ]);
    }

    private static void EmitsRecordTypeDescriptor()
    {
        const string source = "MODULE Demo; TYPE Node = RECORD value: INTEGER; END; VAR node: Node; BEGIN node.value := 42 END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(20, definition.TypeDescriptorBytes);
        TestAssertions.Equal(4, definition.VariableBytes);
        TestAssertions.Equal(0, definition.FixupTypes);

        var image = LoadCompilerModule(definition, "Demo");
        for (var step = 0; step < 8 && image.Memory.Read32(image.Image.DataAddress + 20) != 42; step++)
        {
            image.Cpu.Step();
        }

        TestAssertions.Equal(42u, image.Memory.Read32(image.Image.DataAddress + 20));
    }

    private static void EmitsImportedTypeTestDescriptorLookup()
    {
        var symbols = new OberonSymbolModule(
            "Types",
            0x1234,
            1,
            [new OberonSymbolExport("Node", 5, 0)
            {
                TypeForm = (int)OberonCompilerTypeForm.Record,
                TypeSize = 4,
                RecordFields =
                [
                    new OberonSymbolField(
                        "value",
                        0,
                        (int)OberonCompilerTypeForm.Integer,
                        4,
                        null,
                        null,
                        null)
                ]
            }]);
        var result = new OberonCompilerParser(
            "MODULE Demo; IMPORT Types; TYPE Ref = POINTER TO Types.Node; VAR node: Ref; result: BOOLEAN; BEGIN result := node IS Types.Node; END Demo.",
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = symbols })
            .ParseModule();

        TestAssertions.True(result.Succeeded);
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(20, definition.TypeDescriptorBytes);
        TestAssertions.True(definition.Code.Count > 0);
    }

    private static void ExecutesImportedTypeTestAgainstRuntimeTags()
    {
        var types = CreateRecordHierarchyTypeModule();
        const string source = "MODULE Demo; IMPORT Types; TYPE Ref = POINTER TO Types.Node; DerivedRef = POINTER TO Types.Derived; VAR node: Ref; result: BOOLEAN; BEGIN result := node IS Types.Derived; END Demo.";

        var matchingResult = RunImportedTypeTest(types, source, descriptorOffset: 20);
        var unrelatedResult = RunImportedTypeTest(types, source, descriptorOffset: 0);

        TestAssertions.Equal(1u, matchingResult);
        TestAssertions.Equal(0u, unrelatedResult);
    }

    private static uint RunImportedTypeTest(
        OberonSymbolModule types,
        string source,
        int descriptorOffset)
    {
        var result = new OberonCompilerParser(
            source,
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = types })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        TestAssertions.Equal(40, definition.TypeDescriptorBytes);

        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(types);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        runtime.BindExternal(new OberonExternalModuleBinding("Types", 0x5000, 0x6000, [0]));

        var image = runtime.Start("Demo");
        var variableDataAddress = image.DataAddress + (uint)definition.TypeDescriptorBytes;
        const uint objectAddress = 0x2000;
        const uint typeTagAddress = 0x3000;
        memory.Write32(objectAddress - 8, typeTagAddress);
        memory.Write32(typeTagAddress + 4, image.DataAddress + (uint)descriptorOffset);
        memory.Write32(variableDataAddress, objectAddress);

        for (var step = 0; step < 32; step++)
        {
            cpu.Step();
        }

        return memory.Read32(variableDataAddress + 4);
    }

    private static void EmitsRecordExtensionTypeTestLookup()
    {
        const string source = "MODULE Types; TYPE Base = RECORD value: INTEGER; END; Derived = RECORD (Base) extra: INTEGER; END; Ref = POINTER TO Base; VAR node: Ref; result: BOOLEAN; BEGIN result := node IS Derived; END Types.";
        var parsed = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(parsed);
        var instructions = definition.Code.Select(Risc5Disassembler.Format).ToArray();

        TestAssertions.True(instructions.Any(instruction => instruction == "LDR r0, [r0, 4]"));
        TestAssertions.True(instructions.Any(instruction => instruction == "ADD r1, r13, 0x0014"));
    }

    private static void EmitsRecursivePointerFieldRootReference()
    {
        const string source = "MODULE Types; TYPE Node = RECORD next: POINTER TO Node; value: INTEGER; END; VAR node: Node; BEGIN END Types.";
        var parsed = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(parsed);

        TestAssertions.Equal(24, definition.TypeDescriptorBytes);
        TestAssertions.True(definition.PointerReferences.SequenceEqual([24]));
    }

    private static void EmitsRecursivePointerArrayRootReferences()
    {
        const string source = "MODULE Types; TYPE Node = RECORD next: POINTER TO Node; END; Nodes = ARRAY 2 OF Node; VAR nodes: Nodes; BEGIN END Types.";
        var parsed = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(parsed);

        TestAssertions.True(definition.PointerReferences.SequenceEqual([24, 28]));
        TestAssertions.Equal(2, definition.PointerReferences.Distinct().Count());
    }

    private static void ExecutesExactTypeTestAsTrue()
    {
        const string source = "MODULE Types; TYPE Node = RECORD value: INTEGER; END; Ref = POINTER TO Node; VAR node: Ref; result: BOOLEAN; BEGIN result := node IS Node; END Types.";
        var parsed = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(parsed);
        var loaded = LoadCompilerModule(definition, "Types");

        for (var step = 0; step < 32 && loaded.Memory.Read8(loaded.Image.DataAddress + 24) != 1; step++)
        {
            loaded.Cpu.Step();
        }

        TestAssertions.Equal(1u, loaded.Memory.Read8(loaded.Image.DataAddress + 24));
        TestAssertions.True(definition.Code.Select(Risc5Disassembler.Format)
            .Any(instruction => instruction == "MOV r0, 0x0001"));
    }

    private static void EmitsPointerTypeDescriptorAndRootReferences()
    {
        const string source = "MODULE Demo; TYPE Node = RECORD value: INTEGER; END; Ref = POINTER TO Node; VAR ref: Ref; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(20, definition.TypeDescriptorBytes);
        TestAssertions.True(definition.PointerReferences.SequenceEqual([20]));

        var loaded = LoadCompilerModule(definition, "Demo");
        TestAssertions.Equal(
            loaded.Image.DataAddress + 20,
            loaded.Memory.Read32(loaded.Image.PointerAddress));
    }

    private static void EmitsRecordExtensionTypeFixup()
    {
        const string source = "MODULE Demo; TYPE Base = RECORD value: INTEGER; END; Derived = RECORD (Base) extra: INTEGER; END; VAR item: Derived; BEGIN item.extra := 42 END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(40, definition.TypeDescriptorBytes);
        TestAssertions.Equal(6, definition.FixupTypes);

        var loaded = LoadCompilerModule(definition, "Demo");
        TestAssertions.Equal(loaded.Image.DataAddress, loaded.Memory.Read32(loaded.Image.DataAddress + 24));
        for (var step = 0; step < 8 && loaded.Memory.Read32(loaded.Image.DataAddress + 44) != 42; step++)
        {
            loaded.Cpu.Step();
        }

        TestAssertions.Equal(42u, loaded.Memory.Read32(loaded.Image.DataAddress + 44));
    }

    private static void EmitsNestedRecordTypeDescriptor()
    {
        const string source = "MODULE Demo; TYPE Node = RECORD value: INTEGER; END; Nodes = ARRAY 2 OF Node; VAR nodes: Nodes; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(20, definition.TypeDescriptorBytes);
        TestAssertions.Equal(8, definition.VariableBytes);
    }

    private static void EmitsRecordDescriptorAllocationBoundaries()
    {
        const string source = "MODULE Demo; TYPE Small = RECORD f0, f1, f2, f3, f4, f5: INTEGER; END; Large = RECORD f0, f1, f2, f3, f4, f5, f6: INTEGER; END; VAR small: Small; large: Large; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(40, definition.TypeDescriptorBytes);
        var loaded = LoadCompilerModule(definition, "Demo");
        TestAssertions.Equal(32u, loaded.Memory.Read32(loaded.Image.DataAddress));
        TestAssertions.Equal(64u, loaded.Memory.Read32(loaded.Image.DataAddress + 20));
    }

    private static void EmitsRecursiveRecordDescriptorsDeterministically()
    {
        const string source = "MODULE Demo; TYPE Node = RECORD next: POINTER TO Node; END; VAR node: Node; BEGIN END Demo.";
        var first = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator())
            .EmitRsc(new OberonCompilerParser(source).ParseModule());
        var second = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator())
            .EmitRsc(new OberonCompilerParser(source).ParseModule());

        TestAssertions.True(first.TypeDescriptorPayload.SequenceEqual(second.TypeDescriptorPayload));
        TestAssertions.True(first.PointerReferences.SequenceEqual(second.PointerReferences));
        TestAssertions.True(first.Code.SequenceEqual(second.Code));
    }

    private static void EmitsImportedRecordBaseDescriptor()
    {
        var types = CreateRecordTypeModule();
        const string source = "MODULE Demo; IMPORT Types; TYPE Derived = RECORD (Types.Node) extra: INTEGER; END; VAR item: Derived; BEGIN END Demo.";
        var result = new OberonCompilerParser(
            source,
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = types })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(40, definition.TypeDescriptorBytes);
        TestAssertions.True(definition.FixupTypes > 0);
    }

    private static void ExecutesImportedRecordBaseDescriptor()
    {
        var types = CreateRecordTypeModule();
        const string source = "MODULE Demo; IMPORT Types; TYPE Derived = RECORD (Types.Node) extra: INTEGER; END; VAR item: Derived; BEGIN item.extra := 42; END Demo.";
        var result = new OberonCompilerParser(
            source,
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = types })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Demo" ? bytes : null);
        loader.RegisterExternal(types);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        runtime.BindExternal(new OberonExternalModuleBinding("Types", 0x5000, 0x6000, [0]));

        var image = runtime.Start("Demo");
        var variableDataAddress = image.DataAddress + (uint)definition.TypeDescriptorBytes;
        for (var step = 0; step < 24 && memory.Read32(variableDataAddress + 4) != 42; step++)
        {
            cpu.Step();
        }

        TestAssertions.Equal(42u, memory.Read32(variableDataAddress + 4));
    }

    private static void EmitsImportedRecordExtensionChain()
    {
        var types = CreateRecordTypeModule();
        const string source = "MODULE Demo; IMPORT Types; TYPE Mid = RECORD (Types.Node) mid: INTEGER; END; Leaf = RECORD (Mid) leaf: INTEGER; END; VAR item: Leaf; BEGIN item.leaf := 42; END Demo.";
        var result = new OberonCompilerParser(
            source,
            importedModules: new Dictionary<string, OberonSymbolModule> { ["Types"] = types })
            .ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);

        TestAssertions.Equal(60, definition.TypeDescriptorBytes);
        TestAssertions.True(definition.FixupTypes > 0);
    }

    private static (Memory Memory, Risc5Cpu Cpu, OberonModuleImage Image) LoadCompilerModule(
        RscModule definition,
        string moduleName)
    {
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == moduleName ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        var image = runtime.Start(moduleName);
        return (memory, cpu, image);
    }

    private static void ExecutesCompilerProducedFiveByteArrayCopy()
    {
        const string source = "MODULE Bytes; TYPE Values = ARRAY 5 OF CHAR; VAR source, target: Values; BEGIN target := source; END Bytes.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Bytes" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);

        var image = runtime.Start("Bytes");
        for (var index = 0; index < 5; index++)
        {
            memory.Write8(image.DataAddress + (uint)index, (byte)('A' + index));
        }

        for (var step = 0; step < 48 && memory.Read8(image.DataAddress + 12) != (byte)'E'; step++)
        {
            cpu.Step();
        }

        for (var index = 0; index < 5; index++)
        {
            TestAssertions.Equal(
                (uint)('A' + index),
                memory.Read8(image.DataAddress + 8u + (uint)index));
        }
    }

    private static void ExecutesCompilerProducedSmallByteArrayCopies()
    {
        foreach (var length in new[] { 1, 2, 4 })
        {
            var moduleName = $"Bytes{length}";
            var source = $"MODULE {moduleName}; TYPE Values = ARRAY {length} OF CHAR; VAR source, target: Values; BEGIN target := source; END {moduleName}.";
            var result = new OberonCompilerParser(source).ParseModule();
            var target = result.ModuleScope!.Objects.Single(symbol => symbol.Name == "target");
            var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
            var loader = new OberonModuleLoader(name => name == moduleName
                ? RscModuleWriter.Write(definition)
                : null);
            var memory = new Memory(64 * 1024);
            var cpu = new Risc5Cpu(memory);
            var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
            var image = runtime.Start(moduleName);

            for (var index = 0; index < length; index++)
            {
                memory.Write8(image.DataAddress + (uint)index, (byte)('a' + index));
            }

            for (var step = 0; step < 64; step++)
            {
                cpu.Step();
            }

            for (var index = 0; index < length; index++)
            {
                TestAssertions.Equal(
                    (uint)('a' + index),
                    memory.Read8(image.DataAddress + (uint)target.Value + (uint)index));
            }
        }
    }

    private static void ExecutesCompilerProducedStringBody()
    {
        const string source = "MODULE Strings; VAR text: ARRAY 6 OF CHAR; BEGIN text := \"hello\"; END Strings.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var bytes = RscModuleWriter.Write(definition);
        var loader = new OberonModuleLoader(name => name == "Strings" ? bytes : null);
        var memory = new Memory(64 * 1024);
        var cpu = new Risc5Cpu(memory);
        var runtime = new OberonModuleRuntime(memory, cpu, loader, 0x1000, 0x100);
        var image = runtime.Start("Strings");

        for (var step = 0; step < 64; step++)
        {
            if (memory.Read8(image.DataAddress) == 'h'
                && memory.Read8(image.DataAddress + 1) == 'e'
                && memory.Read8(image.DataAddress + 2) == 'l'
                && memory.Read8(image.DataAddress + 3) == 'l'
                && memory.Read8(image.DataAddress + 4) == 'o'
                && memory.Read8(image.DataAddress + 5) == 0)
            {
                break;
            }

            cpu.Step();
        }

        var text = Enumerable.Range(0, 6)
            .Select(offset => (char)memory.Read8(image.DataAddress + (uint)offset))
            .ToArray();
        TestAssertions.Equal("hello\0", new string(text));
    }

    private static void ExecutesCompilerProducedGlobalAssignmentAfterDescriptor()
    {
        const string source = "MODULE Globals; TYPE Node = RECORD value: INTEGER; END; VAR result: INTEGER; BEGIN result := 42; END Globals.";
        var result = new OberonCompilerParser(source).ParseModule();
        var definition = new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result);
        var loaded = LoadCompilerModule(definition, "Globals");

        for (var step = 0; step < 16 && loaded.Memory.Read32(loaded.Image.DataAddress + 20) != 42; step++)
        {
            loaded.Cpu.Step();
        }

        TestAssertions.Equal(42u, loaded.Memory.Read32(loaded.Image.DataAddress + 20));
    }
}
