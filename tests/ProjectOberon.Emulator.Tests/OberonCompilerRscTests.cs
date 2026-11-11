using ProjectOberon.Emulator;

internal static class OberonCompilerRscTests
{
    public static void Run()
    {
        EmitsMinimalRscModule();
        EmitsStringPayload();
        EmitsImportsAndExportedCommands();
        LoadsCompilerProducedRsc();
        ExecutesCompilerProducedBody();
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
}
