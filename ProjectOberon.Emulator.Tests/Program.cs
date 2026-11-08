using ProjectOberon.Emulator;

var tests = new (string Name, Action Test)[]
{
    ("arithmetic and flags", ArithmeticAndFlags),
    ("integer signedness and subtract borrow", IntegerSignednessAndSubtractBorrow),
    ("branch and link", BranchAndLink),
    ("memory word and byte access", MemoryWordAndByteAccess),
    ("interrupt vector and RTI", InterruptVectorAndRti),
    ("MMIO devices", MmioDevices),
    ("disk image read/write", DiskImageReadWrite),
    ("SPI disk read/write", SpiDiskReadWrite),
    ("machine timer", MachineTimer),
    ("disassembler and breakpoint", DisassemblerAndBreakpoint),
    ("display and input mapping", DisplayAndInputMapping),
    ("Oberon input decoder", OberonInputDecoder),
    ("Oberon kernel sectors", OberonKernelSectors),
    ("Oberon files and riders", OberonFilesAndRiders),
    ("Oberon file directory", OberonFileDirectoryBTree),
    ("Oberon persistent files", OberonPersistentFiles),
    ("Oberon module loader", OberonModuleLoaderGraph),
    ("Oberon module memory image", OberonModuleMemoryImage),
    ("Oberon module fixups", OberonModuleFixups)
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"[ok] {name}");
}

Console.WriteLine("project-oberon-emulator-tests: all tests passed");

static void ArithmeticAndFlags()
{
    var memory = new Memory();
    memory.LoadRomWords(RomWith(MovImmediate(1, 7), MovImmediate(2, 5), Alu(AluOperation.Add, 3, 1, 2)));
    var cpu = new Risc5Cpu(memory);
    cpu.Step(); cpu.Step(); cpu.Step();
    Equal(12u, cpu.Register(3));
    False(cpu.Zero);
    False(cpu.Negative);
}

static void IntegerSignednessAndSubtractBorrow()
{
    var memory = new Memory();
    memory.LoadRomWords(RomWith(MovImmediate(1, 0xffff, true), MovImmediate(2, 2), Alu(AluOperation.Mul, 3, 1, 2)));
    var cpu = new Risc5Cpu(memory);
    cpu.Step(); cpu.Step(); cpu.Step();
    Equal(0xffff_fffeu, cpu.Register(3));
    Equal(0xffff_ffffu, cpu.High);

    memory = new Memory();
    memory.LoadRomWords(RomWith(MovImmediate(1, 3), MovImmediate(2, 5), Alu(AluOperation.Sub, 3, 1, 2)));
    cpu = new Risc5Cpu(memory);
    cpu.Step(); cpu.Step(); cpu.Step();
    Equal(0xffff_fffeu, cpu.Register(3));
    True(cpu.Carry);
}

static void BranchAndLink()
{
    var memory = new Memory();
    memory.LoadRomWords(RomWith(BranchRelative(ConditionCode.Always, 2, true), MovImmediate(1, 0xdead), MovImmediate(1, 0xbeef), MovImmediate(2, 42)));
    var cpu = new Risc5Cpu(memory);
    cpu.Step(); cpu.Step();
    Equal(0u, cpu.Register(1));
    Equal(42u, cpu.Register(2));
    Equal((Risc5Cpu.ResetPc + 1) * 4, cpu.Register(15));
}

static void MemoryWordAndByteAccess()
{
    var memory = new Memory();
    memory.LoadRomWords(RomWith(MovImmediate(1, 0x100), MovImmediate(2, 0xaa), MemoryOp(true, true, 2, 1, 0x20), MemoryOp(false, true, 3, 1, 0x20)));
    var cpu = new Risc5Cpu(memory);
    for (var index = 0; index < 4; index++) cpu.Step();
    Equal(0xaau, cpu.Register(3));
}

static void InterruptVectorAndRti()
{
    var memory = new Memory();
    memory.LoadRomWords(RomWith(0xc700_0021u, 0xc700_0010u));
    memory.Write32(4, 0xc700_0010u);
    var cpu = new Risc5Cpu(memory);
    cpu.Step(); cpu.Step(true); cpu.Step(false);
    Equal(1u, cpu.PcWord);
    cpu.Step(false);
    Equal(Risc5Cpu.ResetPc + 3, cpu.PcWord);
}

static void MmioDevices()
{
    var memory = new Memory();
    memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.Leds, 0x5a);
    Equal(0x5au, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Leds));
    memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.UartData, 'O');
    memory.Write32(DeviceBus.IoBase + (uint)MmioRegister.UartData, 'K');
    Equal("OK", memory.TakeUartOutput());
    memory.EnqueueKeyboard(0x1c);
    True((memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus) & 0x1000_0000u) != 0);
    Equal(0x1cu, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardData));
    memory.SetMouse(123, 456, 5);
    Equal((5u << 24) | (456u << 12) | 123u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.KeyboardStatus) & 0x0fff_ffffu | (5u << 24));
}

static void DiskImageReadWrite()
{
    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"project-oberon-csharp-{Guid.NewGuid():N}.img");
    try
    {
        File.WriteAllBytes(path, new byte[DiskImage.SectorSize * 2]);
        using (var disk = new DiskImage())
        {
            disk.Open(path);
            var sector = new byte[DiskImage.SectorSize];
            sector[0] = 0xa5;
            disk.Write(1, sector);
            Equal(0xa5, disk.Read(1)[0]);
        }
        using (var readOnly = new DiskImage())
        {
            readOnly.Open(path, writable: false);
            var threw = false;
            try { readOnly.Write(0, new byte[DiskImage.SectorSize]); }
            catch (InvalidOperationException) { threw = true; }
            True(threw);
        }
    }
    finally { File.Delete(path); }
}

static void SpiDiskReadWrite()
{
    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"project-oberon-csharp-spi-{Guid.NewGuid():N}.img");
    try
    {
        File.WriteAllBytes(path, new byte[DiskImage.SectorSize * 4]);
        using var disk = new DiskImage();
        disk.Open(path);
        var source = new byte[DiskImage.SectorSize];
        source[0] = 0x12;
        source[DiskImage.SectorSize - 1] = 0x34;
        disk.Write(1, source);

        var memory = new Memory();
        memory.AttachDisk(disk);
        var control = DeviceBus.IoBase + (uint)MmioRegister.SpiControl;
        var data = DeviceBus.IoBase + (uint)MmioRegister.SpiData;
        memory.Write32(control, SpiDevice.Card0);
        SendCommand(memory, data, 17, 1);
        Equal(0x00u, Transfer(memory, data, 0xff));
        Equal(0xfeu, Transfer(memory, data, 0xff));
        Equal(0x12u, Transfer(memory, data, 0xff));
        for (var index = 1; index < DiskImage.SectorSize - 1; index++) _ = Transfer(memory, data, 0xff);
        Equal(0x34u, Transfer(memory, data, 0xff));
        Transfer(memory, data, 0xff);
        Transfer(memory, data, 0xff);

        SendCommand(memory, data, 24, 2);
        Equal(0x00u, Transfer(memory, data, 0xff));
        Transfer(memory, data, 0xfe);
        var replacement = new byte[DiskImage.SectorSize];
        replacement[0] = 0xa5;
        foreach (var value in replacement) Transfer(memory, data, value);
        Transfer(memory, data, 0xff);
        Equal(0x05u, Transfer(memory, data, 0xff));
        Equal(0xa5, disk.Read(2)[0]);
    }
    finally { File.Delete(path); }
}

static void MachineTimer()
{
    var machine = new Machine();
    machine.Run(Machine.TimerInterruptPeriod);
    Equal(1000u, machine.Memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));
}

static void DisassemblerAndBreakpoint()
{
    Equal("BT 337", Risc5Disassembler.Format(0xe700_0151u));
    Equal("MOV r1, 0x0007", Risc5Disassembler.Format(MovImmediate(1, 7)));
    Equal("LDR.B r3, [r1, 32]", Risc5Disassembler.Format(MemoryOp(false, true, 3, 1, 0x20)));

    var machine = new Machine();
    machine.Memory.LoadRomWords(RomWith(MovImmediate(1, 42)));
    var debugger = new MachineDebugger(machine);
    var resetPc = (Risc5Cpu.ResetPc << 2) & Memory.AddressMask;
    debugger.AddBreakpoint(resetPc);
    Equal(0ul, debugger.Run(10));
    Equal(resetPc, machine.Cpu.PcByte);
    True(debugger.RemoveBreakpoint(resetPc));
    Equal(1ul, debugger.Run(1));
    Equal(42u, machine.Cpu.Register(1));
}

static void DisplayAndInputMapping()
{
    Equal(0x000e_7f00u, Display.Base);
    Equal(1024, Display.Width);
    Equal(768, Display.Height);
    Equal(0x29, InputMapping.KeyboardKey(' ')!.Value.Code);
    Equal(0x75, InputMapping.KeyboardKey(0x40000052)!.Value.Code);
    True(InputMapping.KeyboardKey(0x40000052)!.Value.Extended);
    Equal(0x12, InputMapping.KeyboardKey(0x400000e1)!.Value.Code);
    Equal(0x70, InputMapping.KeyboardKey(0x40000049)!.Value.Code);
    Equal(0x05, InputMapping.MouseButtonsFromState(1 | 4));
    Equal(1023, InputMapping.ScaleCoordinate(999, 1000, Display.Width));

    var memory = new Memory();
    memory.EnqueueUartRx((byte)'O');
    Equal(3u, memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.UartControl));
    Equal((uint)'O', memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.UartData));

    var display = new OberonDisplay(memory);
    display.Dot(Display.White, 0, 0, Display.Replace);
    Equal(1u, memory.Read32(Display.Base));
    display.Dot(Display.White, 0, 0, Display.Invert);
    Equal(0u, memory.Read32(Display.Base));
    display.ReplConst(Display.White, 30, 1, 4, 1, Display.Replace);
    Equal(0xc000_0000u, memory.Read32(Display.Base + (uint)Display.RowBytes));
    Equal(0x0000_0003u, memory.Read32(Display.Base + (uint)Display.RowBytes + 4));

    memory.Write8(0x100, 3);
    memory.Write8(0x101, 2);
    memory.Write8(0x102, 0b101);
    memory.Write8(0x103, 0b010);
    display.CopyPattern(Display.White, 0x100, 10, 2, Display.Paint);
    True((memory.Read32(Display.Base + (uint)(2 * Display.RowBytes)) & (1u << 10)) != 0);
    True((memory.Read32(Display.Base + (uint)(3 * Display.RowBytes)) & (1u << 11)) != 0);

    display.CopyBlock(10, 2, 3, 2, 20, 4, Display.Replace);
    True((memory.Read32(Display.Base + (uint)(4 * Display.RowBytes)) & (1u << 20)) != 0);
    True((memory.Read32(Display.Base + (uint)(5 * Display.RowBytes)) & (1u << 21)) != 0);
}

static void OberonInputDecoder()
{
    var keyboard = new OberonKeyboard();
    keyboard.Enqueue(0x1c);
    Equal(1, keyboard.Available());
    True(keyboard.TryRead(out var character));
    Equal('a', character);

    keyboard.Enqueue(0x12);
    keyboard.Enqueue(0x1c);
    keyboard.Enqueue(0xf0);
    keyboard.Enqueue(0x1c);
    keyboard.Enqueue(0xf0);
    keyboard.Enqueue(0x12);
    True(keyboard.TryRead(out character));
    Equal('A', character);
    Equal(0, keyboard.Available());

    keyboard.Enqueue(0x14);
    keyboard.Enqueue(0x21);
    True(keyboard.TryRead(out character));
    Equal('\x03', character);
}

static void OberonKernelSectors()
{
    var memory = new Memory();
    var kernel = new OberonKernel(memory);
    Equal(0, kernel.AllocatedSectors);
    Equal(0x80000u, OberonKernel.PhysicalSector(0));
    Equal(0x80002u, OberonKernel.PhysicalSector(29));
    Equal(64 * 29, kernel.AllocSector(0));
    Equal(1, kernel.AllocatedSectors);
    kernel.MarkSector(100 * 29);
    Equal(2, kernel.AllocatedSectors);
    kernel.FreeSector(100 * 29);
    Equal(1, kernel.AllocatedSectors);
    kernel.SetClock(1234);
    Equal(1234, kernel.Clock);
    memory.AdvanceTime();
    Equal(1, kernel.Time);
}

static void OberonFilesAndRiders()
{
    var file = new OberonFile("Test.Mod", new byte[2]);
    var writer = file.Set();
    writer.WriteInt(unchecked((int)0x1234_5678));
    writer.WriteString("Oberon");
    writer.WriteNum(42);
    writer.WriteNum(300);
    writer.WriteNum(-100);
    Equal(16, file.Length);

    var reader = file.Set();
    True(reader.ReadInt(out var integer));
    Equal(unchecked((int)0x1234_5678), integer);
    Equal("Oberon", reader.ReadString());
    True(reader.ReadNum(out integer));
    Equal(42, integer);
    True(reader.ReadNum(out integer));
    Equal(300, integer);
    True(reader.ReadNum(out integer));
    Equal(-100, integer);
    False(reader.ReadByte(out _));
    True(reader.Eof);
    Equal(16, reader.Position);
}

static void OberonFileDirectoryBTree()
{
    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"project-oberon-directory-{Guid.NewGuid():N}.img");
    try
    {
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
        using var disk = new DiskImage();
        disk.Open(path);
        var kernel = new OberonKernel(new Memory(), disk);
        var directory = new OberonFileDirectory(kernel);
        directory.Initialize();

        for (var index = 0; index < 25; index++) directory.Insert($"File{index:00}", (100 + index) * OberonKernel.LogicalSectorUnit);
        Equal(25, directory.Enumerate().Count());
        Equal(112 * OberonKernel.LogicalSectorUnit, directory.Search("File12"));

        True(directory.Rename("File12", "Renamed"));
        Equal(112 * OberonKernel.LogicalSectorUnit, directory.Search("Renamed"));
        Equal(0, directory.Search("File12"));
        True(directory.Delete("Renamed", out var removed));
        Equal(112 * OberonKernel.LogicalSectorUnit, removed);
        Equal(24, directory.Enumerate().Count());

        for (var index = 0; index < 12; index++) True(directory.Delete($"File{index:00}", out _));
        Equal(12, directory.Enumerate().Count());
    }
    finally
    {
        File.Delete(path);
    }
}

static void OberonPersistentFiles()
{
    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"project-oberon-files-{Guid.NewGuid():N}.img");
    try
    {
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
            file.SetLength(((long)OberonKernel.FileSystemOffset + 512) * DiskImage.SectorSize);
        using var disk = new DiskImage();
        disk.Open(path);
        var kernel = new OberonKernel(new Memory(), disk);
        var files = new OberonFiles(kernel, initializeDirectory: true);
        var created = files.New("Persist.Mod");
        var writer = created.Set();
        writer.WriteString("Project Oberon");
        for (var index = 0; index < 1500; index++) writer.WriteByte((byte)(index % 251));
        files.Register(created);

        var loaded = files.Old("Persist.Mod");
        True(loaded is not null);
        Equal(created.Length, loaded!.Length);
        var reader = loaded.Set();
        Equal("Project Oberon", reader.ReadString());
        for (var index = 0; index < 1500; index++)
        {
            True(reader.ReadByte(out var value));
            Equal((byte)(index % 251), value);
        }
        False(loaded.Writable);

        var reopenedKernel = new OberonKernel(new Memory(), disk);
        var reopenedFiles = new OberonFiles(reopenedKernel);
        True(reopenedFiles.Old("Persist.Mod") is not null);
        True(reopenedFiles.Rename("Persist.Mod", "Renamed.Mod"));
        True(reopenedFiles.Delete("Renamed.Mod"));
        True(reopenedFiles.Old("Renamed.Mod") is null);
    }
    finally
    {
        File.Delete(path);
    }
}

static void OberonModuleLoaderGraph()
{
    var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start"),
        ["B.rsc"] = BuildRsc("B", 200, [], "Run")
    };
    var loader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
    var module = loader.Load("A.rsc");
    Equal("A", module.Name);
    Equal(2, module.Number);
    Equal("B", module.Imports[0].Name);
    Equal("Start", module.FindCommand("Start")!.Name);
    Equal(2, loader.LoadedModules.Count);
    True(ReferenceEquals(module, loader.Load("A")));
    False(loader.Free("B"));
    True(loader.Free("A"));
    True(loader.Free("B"));

    var conflict = new OberonModuleLoader(name => name == "A" ? BuildRsc("A", 100, [("B", 201)], "Start") : modules["B.rsc"]);
    try
    {
        conflict.Load("A");
        throw new InvalidOperationException("expected module key conflict");
    }
    catch (OberonModuleLoadException error)
    {
        Equal(3, error.Code);
    }
    Equal(0, conflict.LoadedModules.Count);

    var cycle = new OberonModuleLoader(name => name == "A"
        ? BuildRsc("A", 100, [("B", 200)], "Start")
        : BuildRsc("B", 200, [("A", 100)], "Run"));
    try
    {
        cycle.Load("A");
        throw new InvalidOperationException("expected module import cycle");
    }
    catch (OberonModuleLoadException error)
    {
        Equal(5, error.Code);
    }
    Equal(0, cycle.LoadedModules.Count);
}

static void OberonModuleMemoryImage()
{
    var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start", code: [0xc700_0010u]),
        ["B.rsc"] = BuildRsc("B", 200, [], "Run",
            typePayload: [0x44, 0x33, 0x22, 0x11], variableBytes: 4,
            stringPayload: [(byte)'x', 0, 0, 0], code: [0xc700_0020u])
    };
    var moduleLoader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
    var memory = new Memory(64 * 1024);
    var cpu = new Risc5Cpu(memory);
    var runtime = new OberonModuleRuntime(memory, cpu, moduleLoader, 0x1000, 0x100);
    var image = runtime.Start("A");
    var imported = runtime.MemoryLoader.LoadedImages.Single(candidate => candidate.Module.Name == "B");

    Equal(imported.DescriptorAddress, memory.Read32(image.ImportAddress));
    Equal(imported.DataAddress, memory.Read32(0x100u + (uint)imported.Module.Number * 4));
    Equal(image.DataAddress, memory.Read32(0x100u + (uint)image.Module.Number * 4));
    Equal(0x11_22_33_44u, memory.Read32(imported.DataAddress));
    Equal(0u, memory.Read32(imported.DataAddress + 4));
    Equal((uint)'x', memory.Read32(imported.DataAddress + 8));
    Equal(0xc700_0020u, memory.Read32(imported.CodeAddress));
    Equal(imported.CodeAddress, imported.FindCommandAddress("Run")!.Value);
    Equal(image.CodeAddress, image.BodyAddress);
    Equal(0x100u, cpu.Register(12));
    Equal((uint)memory.RamSize - 4, cpu.Register(14));
    Equal(image.BodyAddress, cpu.PcByte);
}

static void OberonModuleFixups()
{
    var modules = new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["A.rsc"] = BuildRsc("A", 100, [("B", 200)], "Start",
            typePayload: [0x44, 0x33, 0x22, 0x11, 1, 0, 0, 0],
            code:
            [
                0xc700_0000u,
                0x0010_0001u,
                0,
                0x8b10_0003u,
                0x0000_0100u
            ],
            fixupProgram: 1,
            fixupData: 3,
            fixupTypes: 1),
        ["B.rsc"] = BuildRsc("B", 200, [], "Run",
            typePayload: [0, 0, 0, 0], variableBytes: 4,
            code: [0], entries: [4])
    };
    var moduleLoader = new OberonModuleLoader(name => modules.TryGetValue($"{name}.rsc", out var data) ? data : null);
    var memory = new Memory(64 * 1024);
    var imageLoader = new OberonModuleMemoryLoader(memory, moduleLoader, 0x1000, 0x100);
    var image = imageLoader.Load("A");
    var imported = imageLoader.LoadedImages.Single(candidate => candidate.Module.Name == "B");

    var branchAddress = image.CodeAddress + 4;
    var branchTarget = imported.CodeAddress + (uint)imported.Module.Definition.Entries[0];
    var branchOffset = (uint)(((long)branchTarget - branchAddress - 4) / 4);
    Equal(0xf700_0000u | (branchOffset & 0x00ff_ffffu), memory.Read32(branchAddress));

    var dataFixup = image.CodeAddress + 12;
    var expectedDataInstruction = (((0x8bu * 0x10u) + 12) << 20) | ((uint)imported.Module.Number * 4);
    Equal(expectedDataInstruction, memory.Read32(dataFixup));
    var importedOffset = 4u + imported.CodeAddress - imported.DataAddress;
    Equal(importedOffset, memory.Read32(dataFixup + 4));
    Equal(image.DataAddress, memory.Read32(image.DataAddress + 4));
}

static byte[] BuildRsc(
    string name,
    int key,
    (string Name, int Key)[] imports,
    string command,
    byte[]? typePayload = null,
    int variableBytes = 0,
    byte[]? stringPayload = null,
    uint[]? code = null,
    int[]? entries = null,
    int fixupProgram = 0,
    int fixupData = 0,
    int fixupTypes = 0,
    int bodyOffset = 0)
{
    typePayload ??= [];
    stringPayload ??= [];
    code ??= [0xc700_0000u];
    entries ??= [0];
    var data = new List<byte>();
    AddString(data, name);
    AddInt(data, key);
    data.Add(1);
    AddInt(data, 0);
    foreach (var import in imports)
    {
        AddString(data, import.Name);
        AddInt(data, import.Key);
    }
    AddString(data, string.Empty);
    AddInt(data, typePayload.Length);
    data.AddRange(typePayload);
    AddInt(data, variableBytes);
    AddInt(data, stringPayload.Length);
    data.AddRange(stringPayload);
    AddInt(data, code.Length);
    foreach (var word in code)
    {
        AddInt(data, unchecked((int)word));
    }
    AddString(data, command);
    AddInt(data, 0);
    AddString(data, string.Empty);
    AddInt(data, entries.Length);
    foreach (var entry in entries)
    {
        AddInt(data, entry);
    }
    AddInt(data, -1);
    AddInt(data, fixupProgram);
    AddInt(data, fixupData);
    AddInt(data, fixupTypes);
    AddInt(data, bodyOffset);
    data.Add((byte)'O');
    return data.ToArray();
}

static void AddString(List<byte> data, string value)
{
    data.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
    data.Add(0);
}

static void AddInt(List<byte> data, int value)
{
    data.AddRange(BitConverter.GetBytes(value));
}

static uint Transfer(Memory memory, uint dataAddress, byte value)
{
    memory.Write32(dataAddress, value);
    return memory.Read32(dataAddress);
}

static void SendCommand(Memory memory, uint dataAddress, byte command, uint argument)
{
    Transfer(memory, dataAddress, (byte)(0x40 | command));
    Transfer(memory, dataAddress, (byte)(argument >> 24));
    Transfer(memory, dataAddress, (byte)(argument >> 16));
    Transfer(memory, dataAddress, (byte)(argument >> 8));
    Transfer(memory, dataAddress, (byte)argument);
    Transfer(memory, dataAddress, 0xff);
}

static uint MovImmediate(int a, ushort value, bool negative = false) => InstructionBits.Q | ((uint)a << 24) | (negative ? InstructionBits.V : 0) | value;
static uint Alu(AluOperation op, int a, int b, int c) => ((uint)a << 24) | ((uint)b << 20) | ((uint)op << 16) | (uint)c;
static uint MemoryOp(bool store, bool byteAccess, int a, int b, uint offset) => InstructionBits.P | (store ? InstructionBits.U : 0) | (byteAccess ? InstructionBits.V : 0) | ((uint)a << 24) | ((uint)b << 20) | (offset & InstructionBits.ImmediateMask);
static uint BranchRelative(ConditionCode condition, int displacement, bool link = false) => InstructionBits.P | InstructionBits.Q | InstructionBits.U | (link ? InstructionBits.V : 0) | ((uint)condition << 24) | ((uint)displacement & InstructionBits.BranchDisplacementMask);

static uint[] RomWith(params uint[] words)
{
    var rom = new uint[Memory.RomWords];
    words.CopyTo(rom, 0);
    return rom;
}

static void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual)) throw new InvalidOperationException($"expected {expected}, got {actual}");
}

static void True(bool value) { if (!value) throw new InvalidOperationException("expected true"); }
static void False(bool value) { if (value) throw new InvalidOperationException("expected false"); }
