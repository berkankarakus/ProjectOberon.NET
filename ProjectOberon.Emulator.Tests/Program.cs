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
    ("display and input mapping", DisplayAndInputMapping)
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
