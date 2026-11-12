using ProjectOberon.Emulator;

internal static class OberonCompilerRscTests
{
    public static void Run()
    {
        EmitsMinimalRscModule();
        EmitsStringPayload();
        EmitsImportsAndExportedCommands();
        EmitsExternalProcedureFixup();
        ExecutesExternalProcedureCall();
        EmitsExternalFunctionFixup();
        EmitsExternalRealFunctionExpression();
        EmitsExternalValueParameterFixup();
        EmitsMultipleExternalFixups();
        EmitsExternalVariableDataFixup();
        EmitsExternalVarParameterCall();
        EmitsExternalFixedArrayParameterCall();
        EmitsExternalPointerParameterCall();
        RejectsInvalidExternalImportTables();
        LoadsCompilerProducedRsc();
        ExecutesCompilerProducedBody();
        ExecutesCompilerProducedStringBody();
        RejectsUnsupportedMinimalRscTypes();
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

    private static void RejectsUnsupportedMinimalRscTypes()
    {
        const string source = "MODULE Demo; TYPE Node = RECORD value: INTEGER; END; VAR node: Node; BEGIN END Demo.";
        var result = new OberonCompilerParser(source).ParseModule();

        TestAssertions.Throws<NotSupportedException>(() =>
            new OberonCompilerModuleEmitter(new OberonCompilerCodeGenerator()).EmitRsc(result));
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
}
