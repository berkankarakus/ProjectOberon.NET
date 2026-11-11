using ProjectOberon.Emulator;

internal static partial class DeviceTests
{
    public static void MachineTimer()
    {
        var machine = new Machine();
        machine.Run(Machine.TimerInterruptPeriod);
        TestAssertions.Equal(1000u, machine.Memory.Read32(DeviceBus.IoBase + (uint)MmioRegister.Timer));
    }

    public static void DisassemblerAndBreakpoint()
    {
        TestAssertions.Equal("BT 337", Risc5Disassembler.Format(0xe700_0151u));
        TestAssertions.Equal("MOV r1, 0x0007", Risc5Disassembler.Format(TestFixtures.MovImmediate(1, 7)));
        TestAssertions.Equal("LDR.B r3, [r1, 32]", Risc5Disassembler.Format(TestFixtures.MemoryOp(false, true, 3, 1, 0x20)));

        var machine = new Machine();
        machine.Memory.LoadRomWords(TestFixtures.RomWith(TestFixtures.MovImmediate(1, 42)));
        var debugger = new MachineDebugger(machine);
        var resetPc = (Risc5Cpu.ResetPc << 2) & Memory.AddressMask;
        debugger.AddBreakpoint(resetPc);
        TestAssertions.Equal(0ul, debugger.Run(10));
        TestAssertions.Equal(resetPc, machine.Cpu.PcByte);
        TestAssertions.True(debugger.RemoveBreakpoint(resetPc));
        TestAssertions.Equal(1ul, debugger.Run(1));
        TestAssertions.Equal(42u, machine.Cpu.Register(1));
    }
}
